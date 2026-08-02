using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Redirects authorization errors back to a validated client redirect URI when client_id and
/// redirect_uri are valid, per OIDC Core §3.1.2.6.
/// </summary>
public sealed class RedirectAuthorizationErrorsHandler(
    IOpenIddictApplicationManager applicationManager) : IOpenIddictServerHandler<ApplyAuthorizationResponseContext>
{
    /// <summary>
    /// Gets the default descriptor for this handler.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyAuthorizationResponseContext>()
            .UseScopedHandler<RedirectAuthorizationErrorsHandler>()
            .SetOrder(OpenIddictServerHandlers.Authentication.ApplyAuthorizationResponse<ApplyAuthorizationResponseContext>.Descriptor.Order + SessionManagementConstants.CustomHandlerOrderOffset)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public async ValueTask HandleAsync(ApplyAuthorizationResponseContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Error)
            || context.Request is null
            || context.Response is null
            || string.IsNullOrWhiteSpace(context.Request.ClientId)
            || string.IsNullOrWhiteSpace(context.Request.RedirectUri))
        {
            return;
        }

#pragma warning disable CA2012
        object? application = await applicationManager.FindByClientIdAsync(context.Request.ClientId);
        if (application is null
            || !await applicationManager.ValidateRedirectUriAsync(application, context.Request.RedirectUri))
        {
            return;
        }
#pragma warning restore CA2012

        context.RedirectUri ??= context.Request.RedirectUri;
        context.ResponseMode ??= ResponseModes.Query;

        if (context.Response[Parameters.State] is null && AuthorizationErrorRedirectHelper.ExtractState(context.Request) is { } state)
        {
            context.Response[Parameters.State] = state;
        }
    }
}

/// <summary>
/// Extension methods for authorization error redirect handlers.
/// </summary>
public static class AuthorizationErrorRedirectExtensions
{
    /// <summary>
    /// Adds handlers that keep authorization protocol errors redirect-compatible for valid clients.
    /// </summary>
    public static OpenIddictServerBuilder AddAuthorizationErrorRedirects(this OpenIddictServerBuilder builder)
    {
        builder.Services.AddScoped<RedirectAuthorizationErrorsHandler>();
        builder.AddEventHandler(RedirectAuthorizationErrorsHandler.Descriptor);

        return builder;
    }
}

/// <summary>
/// Middleware that rewrites authorization errors into validated redirects when client_id and
/// redirect_uri are valid, per OIDC Core §3.1.2.6.
/// </summary>
public sealed class AuthorizationErrorRedirectMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Intercepts OpenIddict authorization errors before they are written to the client
    /// and rewrites them as 302 redirects when client_id and redirect_uri are valid.
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
    /// Rewrites authorization endpoint protocol errors into validated redirects when
    /// client_id and redirect_uri are valid, per OIDC Core §3.1.2.6.
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
            || string.IsNullOrWhiteSpace(response.Error)
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
