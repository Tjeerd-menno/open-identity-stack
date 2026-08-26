using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Middleware that rewrites authorization errors into a response delivered to the client's
/// redirect URI when client_id and redirect_uri are valid, per OIDC Core §3.1.2.6.
/// </summary>
public sealed class AuthorizationErrorRedirectMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Intercepts OpenIddict authorization errors before they are written to the client and
    /// re-encodes them using the response mode the request asked for.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IOpenIddictApplicationManager applicationManager)
    {
        if (!HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsPost(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!context.Request.Path.Equals("/connect/authorize", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        Stream originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await next(context);

            AuthorizationErrorResponse? errorResponse =
                await AuthorizationErrorRedirectHelper.TryBuildErrorResponseAsync(context, applicationManager);
            if (errorResponse is null)
            {
                responseBuffer.Position = 0;
                await responseBuffer.CopyToAsync(originalBody, context.RequestAborted);
                return;
            }

            context.Response.Body = originalBody;
            await WriteErrorResponseAsync(context, errorResponse);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, AuthorizationErrorResponse errorResponse)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        if (string.Equals(errorResponse.ResponseMode, ResponseModes.FormPost, StringComparison.Ordinal))
        {
            byte[] document = Encoding.UTF8.GetBytes(errorResponse.BuildFormPostDocument());

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html;charset=UTF-8";
            context.Response.ContentLength = document.Length;
            await context.Response.Body.WriteAsync(document, context.RequestAborted);
            return;
        }

        context.Response.Headers.Location = errorResponse.BuildLocation();
        context.Response.StatusCode = StatusCodes.Status302Found;
        context.Response.ContentLength = 0;
        context.Response.ContentType = null;
    }
}

/// <summary>
/// Extension methods for registering the authorization error redirect middleware.
/// </summary>
public static class AuthorizationErrorRedirectMiddlewareExtensions
{
    /// <summary>
    /// Rewrites authorization endpoint protocol errors into a response delivered to the
    /// registered redirect URI when client_id and redirect_uri are valid, per OIDC Core §3.1.2.6.
    /// </summary>
    public static IApplicationBuilder UseAuthorizationErrorRedirects(this IApplicationBuilder app) =>
        app.UseMiddleware<AuthorizationErrorRedirectMiddleware>();
}

/// <summary>
/// An authorization error that is deliverable to the client, encoded using the response mode
/// the authorization request asked for.
/// </summary>
/// <param name="ResponseMode">The response mode used to deliver the error.</param>
/// <param name="RedirectUri">The validated redirect URI the error is delivered to.</param>
/// <param name="Parameters">The error parameters, in the order they are written.</param>
internal sealed record AuthorizationErrorResponse(
    string ResponseMode,
    string RedirectUri,
    IReadOnlyList<KeyValuePair<string, string?>> Parameters)
{
    /// <summary>
    /// Builds the <c>Location</c> header value for the query and fragment response modes.
    /// </summary>
    internal string BuildLocation() =>
        string.Equals(ResponseMode, ResponseModes.Fragment, StringComparison.Ordinal)
            ? string.Concat(RedirectUri, "#", EncodeParameters())
            : QueryHelpers.AddQueryString(RedirectUri, Parameters);

    /// <summary>
    /// Builds the self-submitting HTML document used by the form_post response mode.
    /// </summary>
    internal string BuildFormPostDocument()
    {
        StringBuilder builder = new StringBuilder()
            .Append("<!DOCTYPE html><html><head><title>Submitting authorization response</title></head>")
            .Append("<body onload=\"document.forms[0].submit()\">")
            .Append("<form method=\"post\" action=\"")
            .Append(HtmlEncoder.Default.Encode(RedirectUri))
            .Append("\">");

        foreach ((string name, string? value) in Parameters)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            builder.Append("<input type=\"hidden\" name=\"")
                .Append(HtmlEncoder.Default.Encode(name))
                .Append("\" value=\"")
                .Append(HtmlEncoder.Default.Encode(value))
                .Append("\"/>");
        }

        return builder
            .Append("<noscript><button type=\"submit\">Continue</button></noscript>")
            .Append("</form></body></html>")
            .ToString();
    }

    private string EncodeParameters() => string.Join(
        '&',
        Parameters
            .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
            .Select(parameter => string.Concat(
                Uri.EscapeDataString(parameter.Key),
                "=",
                Uri.EscapeDataString(parameter.Value!))));
}

internal static class AuthorizationErrorRedirectHelper
{
    internal static async Task<AuthorizationErrorResponse?> TryBuildErrorResponseAsync(
        HttpContext context,
        IOpenIddictApplicationManager applicationManager)
    {
        if (context.Response.StatusCode != StatusCodes.Status400BadRequest)
        {
            return null;
        }

        OpenIddictServerTransaction? transaction =
            context.Features.Get<OpenIddictServerAspNetCoreFeature>()?.Transaction;
        OpenIddictRequest? request = transaction?.Request;
        OpenIddictResponse? response = transaction?.Response;

        if (request is null
            || response is null
            || string.IsNullOrWhiteSpace(response.Error)
            || string.IsNullOrWhiteSpace(request.ClientId)
            || string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            return null;
        }

        // An unrecognised response_mode cannot be honoured, and silently downgrading it to a
        // query redirect would deliver the error somewhere the client is not listening.
        if (ResolveResponseMode(request) is not { } responseMode)
        {
            return null;
        }

#pragma warning disable CA2012
        object? application = await applicationManager.FindByClientIdAsync(request.ClientId, context.RequestAborted);
        if (application is null
            || !await applicationManager.ValidateRedirectUriAsync(application, request.RedirectUri, context.RequestAborted))
        {
            return null;
        }
#pragma warning restore CA2012

        var parameters = new List<KeyValuePair<string, string?>>
        {
            new(Parameters.Error, response.Error),
            new(Parameters.ErrorDescription, response.ErrorDescription),
            new(Parameters.ErrorUri, response.ErrorUri)
        };

        if (ExtractState(request) is { } state)
        {
            parameters.Add(new KeyValuePair<string, string?>(Parameters.State, state));
        }

        return new AuthorizationErrorResponse(responseMode, request.RedirectUri, parameters);
    }

    /// <summary>
    /// Resolves the response mode the error must be encoded with, or <see langword="null"/> when
    /// the request asked for one this middleware cannot render.
    /// </summary>
    internal static string? ResolveResponseMode(OpenIddictRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ResponseMode))
        {
            // Per OAuth 2.0 Multiple Response Type Encoding Practices, response types that return
            // artifacts through the front channel default to the fragment encoding.
            return request.HasResponseType(ResponseTypes.Token)
                || request.HasResponseType(ResponseTypes.IdToken)
                ? ResponseModes.Fragment
                : ResponseModes.Query;
        }

        return request.ResponseMode switch
        {
            ResponseModes.Query or ResponseModes.Fragment or ResponseModes.FormPost => request.ResponseMode,
            _ => null
        };
    }

    internal static string? ExtractState(OpenIddictRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.State))
        {
            return request.State;
        }

        string? requestObject = request.Request;
        if (string.IsNullOrWhiteSpace(requestObject))
        {
            return null;
        }

        string[] segments = requestObject.Split('.');
        if (segments.Length < 2 || string.IsNullOrWhiteSpace(segments[1]))
        {
            return null;
        }

        try
        {
            byte[] payloadBytes = WebEncoders.Base64UrlDecode(segments[1]);
            using var document = JsonDocument.Parse(payloadBytes);
            return document.RootElement.TryGetProperty(Parameters.State, out JsonElement state)
                && state.ValueKind == JsonValueKind.String
                ? state.GetString()
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
