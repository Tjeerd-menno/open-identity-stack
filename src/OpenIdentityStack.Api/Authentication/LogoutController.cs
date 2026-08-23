using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OpenIdentityStack.Infrastructure.Identity;
using AppPermissions = OpenIdentityStack.Application.Authorization.Permissions;

using SharedKernel;
namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Controller for OpenID Connect logout endpoints including Single Logout (SLO).
/// </summary>
[ApiController]
[Route("connect")]
public sealed class LogoutController : ControllerBase
{
    private readonly IProcessLogoutUseCase processLogoutUseCase;
    private readonly IFrontChannelLogoutService frontChannelLogoutService;
    private readonly ISessionRepository sessionRepository;
    private readonly ILogoutNotifier logoutNotifier;
    private readonly IOpenIddictRequestService requestService;

    public LogoutController(
        IProcessLogoutUseCase processLogoutUseCase,
        IFrontChannelLogoutService frontChannelLogoutService,
        ISessionRepository sessionRepository,
        ILogoutNotifier logoutNotifier,
        IOpenIddictRequestService requestService)
    {
        this.processLogoutUseCase = processLogoutUseCase;
        this.frontChannelLogoutService = frontChannelLogoutService;
        this.sessionRepository = sessionRepository;
        this.logoutNotifier = logoutNotifier;
        this.requestService = requestService;
    }

    /// <summary>
    /// OpenID Connect End Session Endpoint.
    /// Processes logout and triggers Single Logout (SLO) to participating clients.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Logout response with optional front-channel logout iframes.</returns>
    [HttpGet("logout")]
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken = default)
    {
        OpenIddictRequest request = this.requestService.GetRequest(this.HttpContext) ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        string? postLogoutRedirectUri = request.PostLogoutRedirectUri;
        string? state = request.State;

        // CRITICAL: Always sign out the authentication cookies to terminate the SSO session.
        // This must happen before any response is sent to ensure the session is properly cleared.
        await this.HttpContext.SignOutAsync("Cookies");
        await this.HttpContext.SignOutAsync("ExternalCookie");

        // Also delete the session management cookie
        this.HttpContext.Response.Cookies.Delete(
            SessionManagementDefaults.SessionCookieName,
            SessionManagementDefaults.CreateSessionCookieOptions());

        SessionId? sessionId = this.GetCurrentSessionId();
        if (sessionId is null)
        {
            // No active session, just redirect if provided
            if (!string.IsNullOrEmpty(postLogoutRedirectUri))
            {
                return this.Redirect(AppendStateParameter(postLogoutRedirectUri, state));
            }

            return this.Ok(new LogoutResponse(true, "No active session", postLogoutRedirectUri, []));
        }

        // The initiating client is not resolved from 'id_token_hint'. Decoding the hint without
        // validating it would let a caller name any client it likes, and validating it through
        // OpenIddict is not implemented (deferred item 3 in
        // docs/reference/DEFERRED-BACKEND-REMEDIATION-ITEMS.md). Until then no client is treated
        // as the initiator, so every client with a logout URI is notified.
        string? initiatingClientId = null;

        // Process the logout
        Result<ProcessLogoutResult> result = await this.processLogoutUseCase.ExecuteAsync(sessionId.Value, initiatingClientId, cancellationToken);

        if (result.IsFailure)
        {
            return this.BadRequest(new LogoutResponse(false, result.Error.Description, null, []));
        }

        // Build response with front-channel logout iframes
        var frontChannelFrames = result.Value.FrontChannelLogoutUrls
            .Select(url => new FrontChannelLogoutIframe(url))
            .ToList();

        var response = new LogoutResponse(
            true,
            "Logout successful",
            postLogoutRedirectUri,
            frontChannelFrames);

        // If there are front-channel logout frames, redirect to the Account logout page
        // which can render the logout view with iframes
        if (frontChannelFrames.Count > 0)
        {
            // For front-channel logout, redirect to a dedicated page that handles iframes
            // Since the cookies are already cleared, clients will be notified through iframes
            // For now, redirect immediately - front-channel logout is handled asynchronously
            if (!string.IsNullOrEmpty(postLogoutRedirectUri))
            {
                return this.Redirect(AppendStateParameter(postLogoutRedirectUri, state));
            }

            return this.Ok(response);
        }

        // No front-channel logout needed, redirect immediately
        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            return this.Redirect(AppendStateParameter(postLogoutRedirectUri, state));
        }

        return this.Ok(response);
    }

    /// <summary>
    /// API endpoint for administrative session logout.
    /// </summary>
    /// <param name="sessionId">The session ID to terminate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("api/admin/sessions/{sessionId}/logout")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Policy = AppPermissions.Sessions.Revoke)]
    [ProducesResponseType(typeof(LogoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminLogout(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        Result<ProcessLogoutResult> result = await this.processLogoutUseCase.ExecuteAsync(new SessionId(sessionId), null, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
            {
                return this.NotFound(new { error = result.Error.Description });
            }
            return this.BadRequest(new { error = result.Error.Description });
        }

        return this.Ok(new LogoutResponse(
            true,
            "Session terminated",
            null,
            []));
    }

    private SessionId? GetCurrentSessionId()
    {
        // Extract session ID from claims if present
        string? sessionIdClaim = this.HttpContext.User?.FindFirst("sid")?.Value
            ?? this.HttpContext.User?.FindFirst("session_id")?.Value;
        if (!string.IsNullOrEmpty(sessionIdClaim) && Guid.TryParse(sessionIdClaim, out Guid sessionGuid))
        {
            return new SessionId(sessionGuid);
        }

        // Session resolution from the session cookie and the OpenIddict authentication context
        // is not implemented. Tracked as deferred item 3 in
        // docs/reference/DEFERRED-BACKEND-REMEDIATION-ITEMS.md; until then a request whose
        // principal carries no 'sid' claim is treated as having no active session.
        return null;
    }

    /// <summary>
    /// Appends the state parameter to the redirect URI if provided.
    /// According to OIDC RP-Initiated Logout spec, the state parameter must be returned to the client.
    /// </summary>
    /// <param name="redirectUri">The base redirect URI.</param>
    /// <param name="state">The state parameter from the logout request.</param>
    /// <returns>The redirect URI with state parameter appended if provided.</returns>
    private static string AppendStateParameter(string redirectUri, string? state)
    {
        if (string.IsNullOrEmpty(state))
        {
            return redirectUri;
        }

        // Use QueryHelpers to properly append the state parameter
        return QueryHelpers.AddQueryString(redirectUri, "state", state);
    }

}

/// <summary>
/// Response from the logout endpoint.
/// </summary>
/// <param name="Success">Whether the logout was successful.</param>
/// <param name="Message">Status message.</param>
/// <param name="PostLogoutRedirectUri">Optional URI to redirect to after logout.</param>
/// <param name="FrontChannelLogoutFrames">Iframes for front-channel logout to participating clients.</param>
public sealed record LogoutResponse(
    bool Success,
    string Message,
    string? PostLogoutRedirectUri,
    IReadOnlyList<FrontChannelLogoutIframe> FrontChannelLogoutFrames);

/// <summary>
/// Represents an iframe for front-channel logout.
/// </summary>
/// <param name="Url">The URL to render in an iframe for front-channel logout.</param>
public sealed record FrontChannelLogoutIframe(string Url);
