using System.Collections.Immutable;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Infrastructure.Identity;

using AppPermissions = OpenIdentityStack.Application.Authorization.Permissions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Minimal-API endpoints that implement the OpenIddict passthrough endpoints
/// (/connect/authorize, /connect/token, /connect/userinfo, /connect/logout,
///  /connect/check_session) and the admin session-logout helper.
/// </summary>
internal static class ConnectEndpoints
{
    private const string LegacySessionIdClaim = "session_id";
    private const string RequestedUserInfoClaim = "requested_userinfo_claim";
    private const string AuthenticationContextClassReferenceClaim = "acr";
    private const string SupportedAcrValue = "1";

    public static IEndpointRouteBuilder MapConnectApi(this IEndpointRouteBuilder app)
    {
        app.MapMethods("connect/authorize", ["GET", "POST"], Authorize);

        app.MapPost("connect/token", Exchange)
            .EnableRateLimiting("TokenEndpoint");

        app.MapMethods("connect/userinfo", ["GET", "POST"], UserInfo);

        app.MapMethods("connect/logout", ["GET", "POST"], Logout)
            .AllowAnonymous();

        app.MapGet("connect/check_session", CheckSession)
            .AllowAnonymous();

        app.MapPost(
                "connect/api/admin/sessions/{sessionId:guid}/logout",
                AdminLogout)
            .RequireAuthorization(
                policy: new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(ctx =>
                        ctx.User.HasClaim("permission", AppPermissions.Sessions.Revoke)
                        || ctx.User.HasClaim("permission", AppPermissions.All))
                    .Build());

        return app;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // /connect/authorize
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> Authorize(
        HttpContext context,
        [Microsoft.AspNetCore.Mvc.FromServices] IOpenIddictRequestService requestService,
        [Microsoft.AspNetCore.Mvc.FromServices] IOpenIddictScopeManager scopeManager,
        [Microsoft.AspNetCore.Mvc.FromServices] IUserRepository userRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler,
        [Microsoft.AspNetCore.Mvc.FromServices] IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler,
        [Microsoft.AspNetCore.Mvc.FromServices] IAddClientSessionUseCase addClientSessionUseCase,
        [Microsoft.AspNetCore.Mvc.FromServices] IValidateSessionQueryHandler validateSessionQueryHandler)
    {
        OpenIddictRequest request = requestService.GetRequest(context) ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        AuthenticateResult cookieAuth = await context.AuthenticateAsync("Cookies");
        ClaimsPrincipal? authenticatedUser = cookieAuth is { Succeeded: true, Principal.Identity.IsAuthenticated: true }
            ? cookieAuth.Principal
            : context.User.Identity?.IsAuthenticated == true ? context.User : null;

        DateTimeOffset? authenticationTime = GetAuthenticationTime(cookieAuth.Properties, authenticatedUser);
        bool isAuthenticated = authenticatedUser?.Identity?.IsAuthenticated == true;
        bool forceLogin = request.HasPromptValue("login");

        if (request.MaxAge is long maxAge)
        {
            forceLogin |= authenticationTime is null
                || DateTimeOffset.UtcNow - authenticationTime.Value > TimeSpan.FromSeconds(maxAge);
        }

        bool promptNone = request.HasPromptValue("none");
        if (promptNone && (forceLogin || !isAuthenticated))
        {
            return Results.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The user is not currently authenticated."
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        if (forceLogin && isAuthenticated)
        {
            await context.SignOutAsync("Cookies");
            await context.SignOutAsync("ExternalCookie");

            string returnUrl = context.Request.PathBase + context.Request.Path + QueryString.Create(
                context.Request.HasFormContentType
                    ? context.Request.Form.ToList()
                    : context.Request.Query.ToList());
            returnUrl = ConsumeFreshLoginParameters(returnUrl, request);

            return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}&fresh=true");
        }

        if (!isAuthenticated)
        {
            string returnUrl = context.Request.PathBase + context.Request.Path + QueryString.Create(
                context.Request.HasFormContentType
                    ? context.Request.Form.ToList()
                    : context.Request.Query.ToList());

            return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        ClaimsPrincipal user = authenticatedUser!;
        string userIdString = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Subject claim not found.");
        identity.AddClaim(new Claim(Claims.Subject, userIdString));

        UserId? userId = TryParseUserId(userIdString);
        Domain.Users.User? persistedUser = userId is { } parsedUserId
            ? await userRepository.GetByIdAsync(parsedUserId)
            : null;

        if (persistedUser?.DisplayName is { Length: > 0 } displayName)
        {
            identity.AddClaim(new Claim(Claims.Name, displayName));
        }
        else if (user.FindFirstValue(ClaimTypes.Name) is { } name)
        {
            identity.AddClaim(new Claim(Claims.Name, name));
        }

        if (!string.IsNullOrWhiteSpace(persistedUser?.Email))
        {
            identity.AddClaim(new Claim(Claims.Email, persistedUser.Email));
            identity.AddClaim(new Claim(Claims.EmailVerified, "true", ClaimValueTypes.Boolean));
        }
        else if (user.FindFirstValue(ClaimTypes.Email) is { } email)
        {
            identity.AddClaim(new Claim(Claims.Email, email));
            identity.AddClaim(new Claim(Claims.EmailVerified, "true", ClaimValueTypes.Boolean));
        }

        if (persistedUser is not null)
        {
            AddPersistedProfileClaims(identity, persistedUser);
        }
        else
        {
            AddPrincipalProfileClaims(identity, user);
        }

        if (authenticationTime is { } authTime)
        {
            SetAuthenticationTimeClaim(identity, authTime);
        }

        if (GetSupportedAcrValue(request) is { } acr)
        {
            identity.AddClaim(new Claim(AuthenticationContextClassReferenceClaim, acr));
        }

        foreach (string requestedClaim in GetRequestedUserInfoClaims(request))
        {
            identity.AddClaim(new Claim(RequestedUserInfoClaim, requestedClaim));
        }

        if (user.FindFirstValue("sid") is { } sidStr && Guid.TryParse(sidStr, out Guid sidGuid))
        {
            var sessionId = new SessionId(sidGuid);
            if (!string.IsNullOrEmpty(request.ClientId))
            {
                await addClientSessionUseCase.ExecuteAsync(
                    new AddClientSessionCommand(sessionId, request.ClientId));
            }

            identity.AddClaim(new Claim("sid", sidStr));
            identity.AddClaim(new Claim(LegacySessionIdClaim, sidStr));
        }
        else if (user.FindFirstValue(LegacySessionIdClaim) is { } legacySid && Guid.TryParse(legacySid, out Guid legacySidGuid))
        {
            var sessionId = new SessionId(legacySidGuid);
            if (!string.IsNullOrEmpty(request.ClientId))
            {
                await addClientSessionUseCase.ExecuteAsync(
                    new AddClientSessionCommand(sessionId, request.ClientId));
            }

            identity.AddClaim(new Claim("sid", legacySid));
            identity.AddClaim(new Claim(LegacySessionIdClaim, legacySid));
        }

        if (userId is { } resolvedUserId)
        {
            Result<IReadOnlyList<RoleDto>> rolesResult =
                await getUserEffectiveRolesQueryHandler.HandleAsync(resolvedUserId);
            if (rolesResult.IsSuccess)
            {
                foreach (RoleDto role in rolesResult.Value)
                {
                    identity.AddClaim(new Claim(Claims.Role, role.Name));
                    foreach (string permission in role.Permissions)
                    {
                        identity.AddClaim(new Claim("permission", permission));
                    }
                }
            }

            Result<IReadOnlyList<GroupClaimDto>> groupClaimsResult =
                await getGroupClaimsForUserQueryHandler.HandleAsync(resolvedUserId);
            if (groupClaimsResult.IsSuccess)
            {
                foreach (GroupClaimDto groupClaim in groupClaimsResult.Value)
                {
                    var claim = new Claim(groupClaim.Type, groupClaim.Value);
                    var dests = new List<string>();
                    if (groupClaim.TokenTarget == TokenTarget.AccessToken || groupClaim.TokenTarget == TokenTarget.Both)
                    {
                        dests.Add(Destinations.AccessToken);
                    }

                    if (groupClaim.TokenTarget == TokenTarget.IdToken || groupClaim.TokenTarget == TokenTarget.Both)
                    {
                        dests.Add(Destinations.IdentityToken);
                    }

                    if (dests.Count > 0)
                    {
                        claim.Properties["destinations"] = string.Join(" ", dests);
                    }

                    identity.AddClaim(claim);
                }
            }
        }

        identity.SetScopes(request.GetScopes());
        identity.SetResources(
            await scopeManager.ListResourcesAsync(identity.GetScopes())
                .ToListAsync()
                .ConfigureAwait(false));
        identity.SetDestinations(GetDestinations);

        return Results.SignIn(
            new ClaimsPrincipal(identity),
            CreateOpenIddictAuthenticationProperties(authenticationTime),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // /connect/token
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> Exchange(
        HttpContext context,
        [Microsoft.AspNetCore.Mvc.FromServices] IOpenIddictRequestService requestService,
        [Microsoft.AspNetCore.Mvc.FromServices] IOpenIddictApplicationManager applicationManager,
        [Microsoft.AspNetCore.Mvc.FromServices] IOpenIddictScopeManager scopeManager,
        [Microsoft.AspNetCore.Mvc.FromServices] IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler,
        [Microsoft.AspNetCore.Mvc.FromServices] IValidateSessionQueryHandler validateSessionQueryHandler,
        [Microsoft.AspNetCore.Mvc.FromServices] IHostEnvironment? environment)
    {
        OpenIddictRequest request = requestService.GetRequest(context) ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsClientCredentialsGrantType())
        {
            object application = await applicationManager.FindByClientIdAsync(request.ClientId!).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The application details cannot be found.");

            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: Claims.Name,
                roleType: Claims.Role);

            string? clientId = await applicationManager.GetClientIdAsync(application);
            identity.AddClaim(new Claim(Claims.Subject, clientId!));
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, clientId!));

            string? displayName = await applicationManager.GetDisplayNameAsync(application);
            if (!string.IsNullOrEmpty(displayName))
            {
                identity.AddClaim(new Claim(Claims.Name, displayName));
            }

            if ((environment is null || environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                && request.GetScopes().Contains("api"))
            {
                identity.AddClaim(new Claim("permission", AppPermissions.All));
            }

            ImmutableArray<string> scopes = request.GetScopes();
            if (scopes.Contains("isotopes:read") || scopes.Contains("api"))
            {
                identity.AddClaim(new Claim(Claims.Role, "isotope:reader"));
            }

            if (scopes.Contains("isotopes:write") || scopes.Contains("api"))
            {
                identity.AddClaim(new Claim(Claims.Role, "isotope:editor"));
            }

            if (scopes.Contains("isotopes:approve") || scopes.Contains("api"))
            {
                identity.AddClaim(new Claim(Claims.Role, "isotope:approver"));
            }

            if (scopes.Contains("isotopes:audit") || scopes.Contains("api"))
            {
                identity.AddClaim(new Claim(Claims.Role, "isotope:auditor"));
            }

            identity.SetScopes(request.GetScopes());
            identity.SetResources(
                await scopeManager.ListResourcesAsync(identity.GetScopes())
                    .ToListAsync()
                    .ConfigureAwait(false));
            identity.SetDestinations(GetDestinations);

            return Results.SignIn(
                new ClaimsPrincipal(identity),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            AuthenticateResult result =
                await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            if (!result.Succeeded)
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The token is no longer valid."
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            string? sessionIdStr = result.Principal.FindFirst("sid")?.Value
                ?? result.Principal.FindFirst(LegacySessionIdClaim)?.Value;

            if (sessionIdStr is not null && Guid.TryParse(sessionIdStr, out Guid sessionIdGuid))
            {
                ValidateSessionResult validateResult = await validateSessionQueryHandler.HandleAsync(
                    new ValidateSessionQuery(new SessionId(sessionIdGuid)));

                if (!validateResult.IsValid && validateResult.Reason != "Session not found")
                {
                    return Results.Forbid(
                        new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                                validateResult.Reason ?? "Session invalid"
                        }),
                        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
                }
            }

            var identity = new ClaimsIdentity(
                result.Principal!.Claims.Where(claim =>
                    !string.Equals(claim.Type, LegacySessionIdClaim, StringComparison.Ordinal)
                    && !string.Equals(claim.Type, Claims.AuthenticationTime, StringComparison.Ordinal)),
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: Claims.Name,
                roleType: Claims.Role);

            identity.SetScopes(result.Principal.GetScopes());
            identity.SetResources(result.Principal.GetResources());

            DateTimeOffset? authenticationTime = GetAuthenticationTime(result.Properties, result.Principal!);
            if (authenticationTime is { } authTime)
            {
                SetAuthenticationTimeClaim(identity, authTime);
            }

            identity.SetDestinations(GetDestinations);
            return Results.SignIn(
                new ClaimsPrincipal(identity),
                CreateOpenIddictAuthenticationProperties(authenticationTime),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // /connect/userinfo
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> UserInfo(HttpContext context)
    {
        AuthenticateResult result =
            await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            return Results.Challenge(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The access token is not valid."
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var claims = new JsonObject();
        claims[Claims.Subject] = result.Principal!.GetClaim(Claims.Subject)!;

        ImmutableHashSet<string> requestedUserInfoClaims = GetRequestedUserInfoClaimsFromPrincipal(result.Principal);

        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Name, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.GivenName, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.FamilyName, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.MiddleName, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Nickname, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.PreferredUsername, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Profile, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Picture, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Website, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Gender, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Birthdate, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Zoneinfo, Scopes.Profile);
        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Locale, Scopes.Profile);
        AddUserInfoIntegerClaim(claims, result.Principal, requestedUserInfoClaims, Claims.UpdatedAt, Scopes.Profile);

        AddUserInfoStringClaim(claims, result.Principal, requestedUserInfoClaims, Claims.Email, Scopes.Email);

        if (result.Principal.HasScope(Scopes.Email) || requestedUserInfoClaims.Contains(Claims.EmailVerified))
        {
            if (result.Principal.GetClaim(Claims.EmailVerified) is { } emailVerified)
            {
                claims[Claims.EmailVerified] = string.Equals(
                    emailVerified, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        return Results.Ok(claims);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // /connect/logout
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> Logout(
        HttpContext context,
        [Microsoft.AspNetCore.Mvc.FromServices] IOpenIddictRequestService requestService,
        [Microsoft.AspNetCore.Mvc.FromServices] IProcessLogoutUseCase processLogoutUseCase,
        [Microsoft.AspNetCore.Mvc.FromServices] IFrontChannelLogoutService frontChannelLogoutService,
        [Microsoft.AspNetCore.Mvc.FromServices] ISessionRepository sessionRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] ILogoutNotifier logoutNotifier,
        CancellationToken cancellationToken)
    {
        OpenIddictRequest request = requestService.GetRequest(context) ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        string? postLogoutRedirectUri = request.PostLogoutRedirectUri;
        string? idTokenHint = request.IdTokenHint;
        string? state = request.State;

        await context.SignOutAsync("Cookies");
        await context.SignOutAsync("ExternalCookie");

        context.Response.Cookies.Delete(
            SessionManagementDefaults.SessionCookieName,
            SessionManagementDefaults.CreateSessionCookieOptions());

        SessionId? sessionId = GetCurrentSessionId(context);
        if (sessionId is null)
        {
            if (!string.IsNullOrEmpty(postLogoutRedirectUri))
            {
                return Results.Redirect(AppendStateParameter(postLogoutRedirectUri, state));
            }

            return Results.Ok(new LogoutResponse(true, "No active session", postLogoutRedirectUri, []));
        }

        string? initiatingClientId = ExtractClientIdFromTokenHint(idTokenHint);
        Result<ProcessLogoutResult> result =
            await processLogoutUseCase.ExecuteAsync(sessionId.Value, initiatingClientId, cancellationToken);

        if (result.IsFailure)
        {
            return Results.BadRequest(new LogoutResponse(false, result.Error.Description, null, []));
        }

        var frontChannelFrames = result.Value.FrontChannelLogoutUrls
            .Select(url => new FrontChannelLogoutIframe(url))
            .ToList();

        var response = new LogoutResponse(
            true, "Logout successful", postLogoutRedirectUri, frontChannelFrames);

        if (frontChannelFrames.Count > 0)
        {
            if (!string.IsNullOrEmpty(postLogoutRedirectUri))
            {
                return Results.Redirect(AppendStateParameter(postLogoutRedirectUri, state));
            }

            return Results.Ok(response);
        }

        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            return Results.Redirect(AppendStateParameter(postLogoutRedirectUri, state));
        }

        return Results.Ok(response);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // /connect/check_session
    // ──────────────────────────────────────────────────────────────────────────

    private static IResult CheckSession()
    {
        string cookieName = System.Web.HttpUtility.JavaScriptStringEncode(
            SessionManagementDefaults.SessionCookieName);

        string html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8" /><title>Check Session</title></head>
            <body>
            <script>
                (function () {
                    "use strict";
                    const sessionCookieName = "{{cookieName}}";
                    function getCookie(name) {
                        const cookies = document.cookie ? document.cookie.split(";") : [];
                        for (const cookie of cookies) {
                            const [key, ...rest] = cookie.trim().split("=");
                            if (key === name) return decodeURIComponent(rest.join("="));
                        }
                        return null;
                    }
                    function base64UrlEncode(buffer) {
                        const bytes = new Uint8Array(buffer);
                        let binary = "";
                        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
                        return btoa(binary).replace(/\+/g,"-").replace(/\//g,"_").replace(/=+$/,"");
                    }
                    async function computeSessionState(clientId, origin, sessionId, salt) {
                        const payload = `${clientId}${origin}${sessionId}${salt}`;
                        const hash = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(payload));
                        return `${base64UrlEncode(hash)}.${salt}`;
                    }
                    window.addEventListener("message", async (event) => {
                        if (typeof event.data !== "string") return;
                        const parts = event.data.split(" ");
                        if (parts.length !== 2) { event.source?.postMessage("error", event.origin); return; }
                        const [clientId, sessionState] = parts;
                        const salt = sessionState.split(".")[1];
                        if (!clientId || !salt) { event.source?.postMessage("error", event.origin); return; }
                        const sessionId = getCookie(sessionCookieName);
                        if (!sessionId) { event.source?.postMessage("changed", event.origin); return; }
                        const expected = await computeSessionState(clientId, event.origin, sessionId, salt);
                        event.source?.postMessage(expected === sessionState ? "unchanged" : "changed", event.origin);
                    });
                })();
            </script>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // /connect/api/admin/sessions/{sessionId}/logout
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> AdminLogout(
        Guid sessionId,
        [Microsoft.AspNetCore.Mvc.FromServices] IProcessLogoutUseCase processLogoutUseCase,
        CancellationToken cancellationToken)
    {
        Result<ProcessLogoutResult> result =
            await processLogoutUseCase.ExecuteAsync(new SessionId(sessionId), null, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
            {
                return Results.NotFound(new { error = result.Error.Description });
            }

            return Results.BadRequest(new { error = result.Error.Description });
        }

        return Results.Ok(new LogoutResponse(true, "Session terminated", null, []));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Shared helpers (ported from AuthorizationController / LogoutController)
    // ──────────────────────────────────────────────────────────────────────────

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        if (IsInternalTokenStateClaim(claim.Type))
        {
            yield break;
        }

        if (claim.Properties.TryGetValue("destinations", out string? destinations)
            && !string.IsNullOrEmpty(destinations))
        {
            foreach (string dest in destinations.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                yield return dest;
            }

            yield break;
        }

        switch (claim.Type)
        {
            case RequestedUserInfoClaim:
                yield return Destinations.AccessToken;
                yield break;

            case Claims.Name
                or Claims.PreferredUsername
                or Claims.GivenName
                or Claims.FamilyName
                or Claims.MiddleName
                or Claims.Nickname
                or Claims.Profile
                or Claims.Picture
                or Claims.Website
                or Claims.Gender
                or Claims.Birthdate
                or Claims.Zoneinfo
                or Claims.Locale
                or Claims.UpdatedAt:
                if (claim.Subject?.HasScope(Scopes.Profile) == true
                    || claim.Subject?.HasClaim(RequestedUserInfoClaim, claim.Type) == true)
                {
                    yield return Destinations.AccessToken;
                }

                if (claim.Subject?.HasScope(Scopes.Profile) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Email or Claims.EmailVerified:
                if (claim.Subject?.HasScope(Scopes.Email) == true
                    || claim.Subject?.HasClaim(RequestedUserInfoClaim, claim.Type) == true)
                {
                    yield return Destinations.AccessToken;
                }

                if (claim.Subject?.HasScope(Scopes.Email) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;
                if (claim.Subject?.HasScope(Scopes.Roles) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case "sid":
                if (claim.Subject?.HasScope(Scopes.OpenId) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.AuthenticationTime:
                if (claim.Subject?.HasScope(Scopes.OpenId) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case AuthenticationContextClassReferenceClaim:
                if (claim.Subject?.HasScope(Scopes.OpenId) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }

    private static bool IsInternalTokenStateClaim(string claimType) =>
        claimType is LegacySessionIdClaim or "oi_au_id" or "oi_tkn_id"
        || claimType.StartsWith("oi_", StringComparison.Ordinal);

    private static ImmutableHashSet<string> GetRequestedUserInfoClaims(OpenIddictRequest request)
    {
        if (!request.TryGetParameter(Parameters.Claims, out OpenIddictParameter parameter)
            || OpenIddictParameter.IsNullOrEmpty(parameter))
        {
            return ImmutableHashSet<string>.Empty;
        }

        return ParseRequestedClaims(parameter, "userinfo");
    }

    private static ImmutableHashSet<string> GetRequestedUserInfoClaimsFromPrincipal(ClaimsPrincipal principal) =>
        principal.FindAll(RequestedUserInfoClaim)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToImmutableHashSet(StringComparer.Ordinal);

    private static UserId? TryParseUserId(string rawUserId) =>
        Guid.TryParse(rawUserId, out Guid id) ? new UserId(id) : null;

    private static void AddPersistedProfileClaims(ClaimsIdentity identity, Domain.Users.User user)
    {
        AddStringClaim(identity, Claims.GivenName, user.GivenName);
        AddStringClaim(identity, Claims.FamilyName, user.FamilyName);
        AddStringClaim(identity, Claims.MiddleName, user.MiddleName);
        AddStringClaim(identity, Claims.Nickname, user.Nickname);
        AddStringClaim(identity, Claims.PreferredUsername, user.PreferredUsername);
        AddStringClaim(identity, Claims.Profile, user.Profile);
        AddStringClaim(identity, Claims.Picture, user.Picture);
        AddStringClaim(identity, Claims.Website, user.Website);
        AddStringClaim(identity, Claims.Gender, user.Gender);
        AddStringClaim(identity, Claims.Birthdate, user.Birthdate);
        AddStringClaim(identity, Claims.Zoneinfo, user.ZoneInfo);
        AddStringClaim(identity, Claims.Locale, user.Locale);

        DateTimeOffset updatedAt = user.ModifiedAt ?? user.CreatedAt;
        identity.AddClaim(new Claim(
            Claims.UpdatedAt,
            updatedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));
    }

    private static void AddPrincipalProfileClaims(ClaimsIdentity identity, ClaimsPrincipal principal)
    {
        AddStringClaim(identity, Claims.GivenName, principal.FindFirstValue(Claims.GivenName));
        AddStringClaim(identity, Claims.FamilyName, principal.FindFirstValue(Claims.FamilyName));
        AddStringClaim(identity, Claims.MiddleName, principal.FindFirstValue(Claims.MiddleName));
        AddStringClaim(identity, Claims.Nickname, principal.FindFirstValue(Claims.Nickname));
        AddStringClaim(identity, Claims.PreferredUsername, principal.FindFirstValue(Claims.PreferredUsername));
        AddStringClaim(identity, Claims.Profile, principal.FindFirstValue(Claims.Profile));
        AddStringClaim(identity, Claims.Picture, principal.FindFirstValue(Claims.Picture));
        AddStringClaim(identity, Claims.Website, principal.FindFirstValue(Claims.Website));
        AddStringClaim(identity, Claims.Gender, principal.FindFirstValue(Claims.Gender));
        AddStringClaim(identity, Claims.Birthdate, principal.FindFirstValue(Claims.Birthdate));
        AddStringClaim(identity, Claims.Zoneinfo, principal.FindFirstValue(Claims.Zoneinfo));
        AddStringClaim(identity, Claims.Locale, principal.FindFirstValue(Claims.Locale));

        if (principal.FindFirstValue(Claims.UpdatedAt) is { Length: > 0 } updatedAt)
        {
            identity.AddClaim(new Claim(Claims.UpdatedAt, updatedAt, ClaimValueTypes.Integer64));
        }
    }

    private static void AddStringClaim(ClaimsIdentity identity, string claimType, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            identity.AddClaim(new Claim(claimType, value));
        }
    }

    private static void AddUserInfoStringClaim(
        JsonObject claims,
        ClaimsPrincipal principal,
        ImmutableHashSet<string> requestedClaims,
        string claimType,
        string requiredScope)
    {
        if ((principal.HasScope(requiredScope) || requestedClaims.Contains(claimType))
            && principal.GetClaim(claimType) is { } value)
        {
            claims[claimType] = value;
        }
    }

    private static void AddUserInfoIntegerClaim(
        JsonObject claims,
        ClaimsPrincipal principal,
        ImmutableHashSet<string> requestedClaims,
        string claimType,
        string requiredScope)
    {
        if (!(principal.HasScope(requiredScope) || requestedClaims.Contains(claimType)))
        {
            return;
        }

        if (principal.GetClaim(claimType) is { } value
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
        {
            claims[claimType] = number;
        }
    }

    private static string? GetSupportedAcrValue(OpenIddictRequest request)
    {
        if (!request.TryGetParameter(Parameters.AcrValues, out OpenIddictParameter parameter)
            || OpenIddictParameter.IsNullOrEmpty(parameter)
            || parameter.ToString() is not { Length: > 0 } acrValues)
        {
            return null;
        }

        return acrValues
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(v => string.Equals(v, SupportedAcrValue, StringComparison.Ordinal));
    }

    private static ImmutableHashSet<string> ParseRequestedClaims(OpenIddictParameter parameter, string sectionName)
    {
        JsonElement? root = GetJsonElement(parameter);
        if (root is not { ValueKind: JsonValueKind.Object } documentRoot
            || !documentRoot.TryGetProperty(sectionName, out JsonElement section)
            || section.ValueKind != JsonValueKind.Object)
        {
            return ImmutableHashSet<string>.Empty;
        }

        return section.EnumerateObject()
            .Select(property => property.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    private static JsonElement? GetJsonElement(OpenIddictParameter parameter)
    {
        object? rawValue = parameter.GetRawValue();
        if (rawValue is JsonElement element)
        {
            return element;
        }

        if (parameter.ToString() is not { Length: > 0 } rawText)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawText);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? GetAuthenticationTime(
        AuthenticationProperties? properties,
        ClaimsPrincipal? principal)
    {
        if (properties?.IssuedUtc is { } issuedUtc)
        {
            return issuedUtc;
        }

        return principal is null ? null : GetAuthenticationTime(principal);
    }

    private static DateTimeOffset? GetAuthenticationTime(ClaimsPrincipal principal)
    {
        string? rawValue = principal.FindFirstValue(Claims.AuthenticationTime);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long secs)
            ? DateTimeOffset.FromUnixTimeSeconds(secs)
            : null;
    }

    private static void SetAuthenticationTimeClaim(ClaimsIdentity identity, DateTimeOffset authTime)
    {
        foreach (Claim claim in identity.FindAll(Claims.AuthenticationTime).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(
            Claims.AuthenticationTime,
            authTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));
    }

    private static AuthenticationProperties CreateOpenIddictAuthenticationProperties(
        DateTimeOffset? authenticationTime)
    {
        AuthenticationProperties properties = new();
        properties.IssuedUtc = authenticationTime;
        return properties;
    }

    private static string ConsumeFreshLoginParameters(string returnUrl, OpenIddictRequest request)
    {
        int queryStart = returnUrl.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0)
        {
            return returnUrl;
        }

        string path = returnUrl[..queryStart];
        string query = returnUrl[(queryStart + 1)..];
        Dictionary<string, StringValues> parameters = new(
            QueryHelpers.ParseQuery(query),
            StringComparer.OrdinalIgnoreCase);

        if (request.HasPromptValue("login") && parameters.TryGetValue(Parameters.Prompt, out StringValues promptValues))
        {
            string[] remainingPrompts = promptValues
                .SelectMany(v => v?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [])
                .Where(v => !string.Equals(v, "login", StringComparison.Ordinal))
                .ToArray();

            if (remainingPrompts.Length == 0)
            {
                parameters.Remove(Parameters.Prompt);
            }
            else
            {
                parameters[Parameters.Prompt] = string.Join(' ', remainingPrompts);
            }
        }

        if (request.MaxAge == 0)
        {
            parameters.Remove(Parameters.MaxAge);
        }

        IEnumerable<KeyValuePair<string, string?>> queryParameters = parameters
            .SelectMany(p => p.Value, (p, v) => new KeyValuePair<string, string?>(p.Key, v));

        return path + QueryString.Create(queryParameters);
    }

    private static SessionId? GetCurrentSessionId(HttpContext context)
    {
        string? sessionIdClaim = context.User?.FindFirst("sid")?.Value
            ?? context.User?.FindFirst("session_id")?.Value;

        if (!string.IsNullOrEmpty(sessionIdClaim) && Guid.TryParse(sessionIdClaim, out Guid sessionGuid))
        {
            return new SessionId(sessionGuid);
        }

        return null;
    }

    private static string? ExtractClientIdFromTokenHint(string? idTokenHint)
    {
        if (string.IsNullOrEmpty(idTokenHint))
        {
            return null;
        }

        return null;
    }

    private static string AppendStateParameter(string redirectUri, string? state)
    {
        if (string.IsNullOrEmpty(state))
        {
            return redirectUri;
        }

        return QueryHelpers.AddQueryString(redirectUri, "state", state);
    }
}
