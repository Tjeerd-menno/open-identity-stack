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
/// Middleware that rewrites unsupported request-object errors into authorization redirects.
/// </summary>
public sealed class AuthorizationErrorRedirectMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Intercepts OpenIddict authorization errors before they are written to the client.
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

            string? redirectLocation = await AuthorizationErrorRedirectHelper.TryBuildRedirectLocationAsync(context, applicationManager);
            if (redirectLocation is null)
            {
                responseBuffer.Position = 0;
                await responseBuffer.CopyToAsync(originalBody, context.RequestAborted);
                return;
            }

            context.Response.Body = originalBody;
            context.Response.Headers.Location = redirectLocation;
            context.Response.StatusCode = StatusCodes.Status302Found;
            context.Response.ContentLength = 0;
            context.Response.ContentType = null;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}

/// <summary>
/// Extension methods for registering the authorization error redirect middleware.
/// </summary>
public static class AuthorizationErrorRedirectMiddlewareExtensions
{
    /// <summary>
    /// Rewrites specific authorization endpoint protocol errors into validated redirects.
    /// </summary>
    public static IApplicationBuilder UseAuthorizationErrorRedirects(this IApplicationBuilder app) =>
        app.UseMiddleware<AuthorizationErrorRedirectMiddleware>();
}

internal static class AuthorizationErrorRedirectHelper
{
    internal static async Task<string?> TryBuildRedirectLocationAsync(
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
            || !string.Equals(response.Error, Errors.RequestNotSupported, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.Request)
            || string.IsNullOrWhiteSpace(request.ClientId)
            || string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            return null;
        }

#pragma warning disable CA2012
        object? application = await applicationManager.FindByClientIdAsync(request.ClientId);
        if (application is null
            || !await applicationManager.ValidateRedirectUriAsync(application, request.RedirectUri))
        {
            return null;
        }
#pragma warning restore CA2012

        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [Parameters.Error] = response.Error,
            [Parameters.ErrorDescription] = response.ErrorDescription,
            [Parameters.ErrorUri] = response.ErrorUri
        };

        if (ExtractState(request) is { } state)
        {
            parameters[Parameters.State] = state;
        }

        return QueryHelpers.AddQueryString(request.RedirectUri, parameters);
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
