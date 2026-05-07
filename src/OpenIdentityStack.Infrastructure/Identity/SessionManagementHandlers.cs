using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Constants for OpenID Connect session management.
/// </summary>
public static class SessionManagementConstants
{
    /// <summary>
    /// The session_state parameter name used in authorization responses.
    /// </summary>
    public const string SessionState = "session_state";

    /// <summary>
    /// Order offset for custom session management handlers to run after built-in handlers.
    /// </summary>
    internal const int CustomHandlerOrderOffset = 1000;
}

/// <summary>
/// Adds the check_session_iframe metadata entry to the discovery document.
/// </summary>
public sealed class AddCheckSessionMetadataHandler : IOpenIddictServerHandler<HandleConfigurationRequestContext>
{
    /// <summary>
    /// Gets the default descriptor for this handler.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<HandleConfigurationRequestContext>()
            .UseSingletonHandler<AddCheckSessionMetadataHandler>()
            .SetOrder(OpenIddictServerHandlers.Discovery.HandleConfigurationRequest.Descriptor.Order + SessionManagementConstants.CustomHandlerOrderOffset)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public ValueTask HandleAsync(HandleConfigurationRequestContext context)
    {
        if (context.Issuer is null)
        {
            return ValueTask.CompletedTask;
        }

        var checkSessionUri = new Uri(context.Issuer, "connect/check_session");
        context.Metadata["check_session_iframe"] = checkSessionUri.AbsoluteUri;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Attaches the session_state parameter to authorization responses.
/// </summary>
public sealed class AttachSessionStateHandler : IOpenIddictServerHandler<ApplyAuthorizationResponseContext>
{
    /// <summary>
    /// Gets the default descriptor for this handler.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ApplyAuthorizationResponseContext>()
            .UseScopedHandler<AttachSessionStateHandler>()
            .SetOrder(OpenIddictServerHandlers.Authentication.ApplyAuthorizationResponse<ApplyAuthorizationResponseContext>.Descriptor.Order + SessionManagementConstants.CustomHandlerOrderOffset)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public ValueTask HandleAsync(ApplyAuthorizationResponseContext context)
    {
        if (context.Request is null || context.Response is null)
        {
            return ValueTask.CompletedTask;
        }

        string? clientId = context.Request.ClientId;
        string? redirectUri = context.Request.RedirectUri;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
        {
            return ValueTask.CompletedTask;
        }

        if (context.Transaction.GetProperty<HttpContext>(typeof(HttpContext).FullName!) is not { } httpContext)
        {
            return ValueTask.CompletedTask;
        }

        if (!httpContext.Request.Cookies.TryGetValue(SessionManagementDefaults.SessionCookieName, out string? sessionCookie) ||
            string.IsNullOrWhiteSpace(sessionCookie))
        {
            return ValueTask.CompletedTask;
        }

        string origin = new Uri(redirectUri).GetLeftPart(UriPartial.Authority);
        string salt = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        string sessionState = ComputeSessionState(clientId, origin, sessionCookie, salt);

        context.Response[SessionManagementConstants.SessionState] = sessionState;
        return ValueTask.CompletedTask;
    }

    private static string ComputeSessionState(string clientId, string origin, string sessionCookie, string salt)
    {
        string payload = $"{clientId}{origin}{sessionCookie}{salt}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        string digest = WebEncoders.Base64UrlEncode(hash);
        return $"{digest}.{salt}";
    }
}

/// <summary>
/// Extension methods for registering session management handlers.
/// </summary>
public static class SessionManagementExtensions
{
    /// <summary>
    /// Adds session management handlers to the OpenIddict server pipeline.
    /// </summary>
    public static OpenIddictServerBuilder AddSessionManagement(this OpenIddictServerBuilder builder)
    {
        // Register the implementation types in DI so OpenIddict can resolve them
        builder.Services.AddSingleton<AddCheckSessionMetadataHandler>();
        builder.Services.AddScoped<AttachSessionStateHandler>();

        builder.AddEventHandler(AddCheckSessionMetadataHandler.Descriptor);
        builder.AddEventHandler(AttachSessionStateHandler.Descriptor);
        return builder;
    }
}
