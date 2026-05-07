using System.Collections.Immutable;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common; // For SessionId
using OpenIdentityStack.Domain.Groups; // For TokenTarget
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OpenIdentityStack.Application.Roles.Queries;

using SharedKernel;
namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Controller handling OpenIddict authorization and token endpoints.
/// </summary>
[ApiController]
public class AuthorizationController : ControllerBase
{
    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IOpenIddictScopeManager scopeManager;
    private readonly IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler;
    private readonly IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler;
    private readonly IAddClientSessionUseCase addClientSessionUseCase;
    private readonly IValidateSessionQueryHandler validateSessionQueryHandler;
    private readonly IOpenIddictRequestService requestService;
    private readonly IHostEnvironment? environment;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler,
        IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler,
        IAddClientSessionUseCase addClientSessionUseCase,
        IValidateSessionQueryHandler validateSessionQueryHandler,
        IOpenIddictRequestService requestService,
        IHostEnvironment? environment = null)
    {
        this.applicationManager = applicationManager;
        this.scopeManager = scopeManager;
        this.getUserEffectiveRolesQueryHandler = getUserEffectiveRolesQueryHandler;
        this.getGroupClaimsForUserQueryHandler = getGroupClaimsForUserQueryHandler;
        this.addClientSessionUseCase = addClientSessionUseCase;
        this.validateSessionQueryHandler = validateSessionQueryHandler;
        this.requestService = requestService;
        this.environment = environment;
    }

    /// <summary>
    /// Handles the authorization endpoint for authorization code flow.
    /// </summary>
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        OpenIddictRequest request = this.requestService.GetRequest(this.HttpContext) ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // Check if prompt=login was requested - this forces re-authentication
        bool forceLogin = request.HasPromptValue("login");

        // Check max_age - if 0, treat as force login
        if (request.MaxAge == 0)
        {
            forceLogin = true;
        }

        // If prompt=login is requested, sign out current session and force fresh login
        if (forceLogin && this.User.Identity?.IsAuthenticated == true)
        {
            await this.HttpContext.SignOutAsync("Cookies");
            await this.HttpContext.SignOutAsync("ExternalCookie");

            // Preserve the full authorization request in the return URL
            string returnUrl = this.Request.PathBase + this.Request.Path + QueryString.Create(
                this.Request.HasFormContentType ? this.Request.Form.ToList() : this.Request.Query.ToList());

            // Pass fresh=true to indicate we need a fresh external login
            return this.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}&fresh=true");
        }

        // If the user is not authenticated, redirect to login page
        if (this.User.Identity?.IsAuthenticated != true)
        {
            // Preserve the full authorization request in the return URL
            string returnUrl = this.Request.PathBase + this.Request.Path + QueryString.Create(
                this.Request.HasFormContentType ? this.Request.Form.ToList() : this.Request.Query.ToList());

            return this.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        // Create the claims-based identity that will be used by OpenIddict
        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        // Add the claims that will be persisted in the tokens
        string userIdString = this.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Subject claim not found.");
        identity.AddClaim(new System.Security.Claims.Claim(Claims.Subject, userIdString));

        if (this.User.FindFirstValue(ClaimTypes.Name) is { } name)
        {
            identity.AddClaim(new Claim(Claims.Name, name));
        }

        if (this.User.FindFirstValue(ClaimTypes.Email) is { } email)
        {
            identity.AddClaim(new Claim(Claims.Email, email));
        }

        if (this.User.FindFirstValue("sid") is { } sessionIdStr && Guid.TryParse(sessionIdStr, out Guid sessionIdGuid))
        {
            var sessionId = new SessionId(sessionIdGuid);

            if (!string.IsNullOrEmpty(request.ClientId))
            {
                await this.addClientSessionUseCase.ExecuteAsync(new AddClientSessionCommand(
                    sessionId,
                    request.ClientId));
            }

            identity.AddClaim(new Claim("sid", sessionIdStr));
            identity.AddClaim(new Claim("session_id", sessionIdStr));
        }
        else if (this.User.FindFirstValue("session_id") is { } legacySessionIdStr && Guid.TryParse(legacySessionIdStr, out Guid legacySessionIdGuid))
        {
            var sessionId = new SessionId(legacySessionIdGuid);

            if (!string.IsNullOrEmpty(request.ClientId))
            {
                await this.addClientSessionUseCase.ExecuteAsync(new AddClientSessionCommand(
                    sessionId,
                    request.ClientId));
            }

            identity.AddClaim(new Claim("sid", legacySessionIdStr));
            identity.AddClaim(new Claim("session_id", legacySessionIdStr));
        }

        // Add role claims (direct + group mapped)
        if (Guid.TryParse(userIdString, out Guid userIdGuid))
        {
             var userId = new UserId(userIdGuid);

            // 1. Roles and Permissions
            Result<IReadOnlyList<RoleDto>> rolesResult = await this.getUserEffectiveRolesQueryHandler.HandleAsync(userId);
             if (rolesResult.IsSuccess)
             {
                 foreach (RoleDto role in rolesResult.Value)
                 {
                     identity.AddClaim(new Claim(Claims.Role, role.Name));
                     
                     // Add permission claims from each role
                     foreach (string permission in role.Permissions)
                     {
                         identity.AddClaim(new Claim("permission", permission));
                     }
                 }
             }

            // 2. Group Claims
            Result<IReadOnlyList<GroupClaimDto>> groupClaimsResult = await this.getGroupClaimsForUserQueryHandler.HandleAsync(userId);
             if (groupClaimsResult.IsSuccess)
             {
                 foreach (GroupClaimDto groupClaim in groupClaimsResult.Value)
                 {
                     var claim = new Claim(groupClaim.Type, groupClaim.Value);
                     
                     // Helper to map TokenTarget to destinations string
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

        // Allow all claims to be added in the access tokens
        identity.SetScopes(request.GetScopes());
        identity.SetResources(await this.scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync().ConfigureAwait(false));

        identity.SetDestinations(GetDestinations);

        return this.SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handles the token endpoint for exchanging authorization codes or client credentials.
    /// </summary>
    [HttpPost("~/connect/token")]
    [EnableRateLimiting("TokenEndpoint")]
    public async Task<IActionResult> Exchange()
    {
        OpenIddictRequest request = this.requestService.GetRequest(this.HttpContext) ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsClientCredentialsGrantType())
        {
            // For client credentials, use the client ID as the subject
            object application = await this.applicationManager.FindByClientIdAsync(request.ClientId!).ConfigureAwait(false) ??
                throw new InvalidOperationException("The application details cannot be found.");

            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: Claims.Name,
                roleType: Claims.Role);

            string? clientId = await this.applicationManager.GetClientIdAsync(application);
            identity.AddClaim(new Claim(Claims.Subject, clientId!));
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, clientId!));

            string? displayName = await this.applicationManager.GetDisplayNameAsync(application);
            if (!string.IsNullOrEmpty(displayName))
            {
                identity.AddClaim(new Claim(Claims.Name, displayName));
            }

            if ((this.environment is null || this.environment.IsDevelopment() || this.environment.IsEnvironment("Testing"))
                && request.GetScopes().Contains("api"))
            {
                identity.AddClaim(new Claim("permission", OpenIdentityStack.Application.Authorization.Permissions.All));
            }

            // Add isotope-related roles based on requested scopes
            // These roles are required by TraceableIsotopes.Api authorization policies
            ImmutableArray<string> scopes = request.GetScopes();

            // isotopes:read grants the Reader role
            if (scopes.Contains("isotopes:read") || scopes.Contains("api"))
            {
                identity.AddClaim(new Claim(Claims.Role, "isotope:reader"));
            }

            // isotopes:write grants the Editor role
            if (scopes.Contains("isotopes:write") || scopes.Contains("api"))
            {
                identity.AddClaim(new Claim(Claims.Role, "isotope:editor"));
            }

            // isotopes:approve grants the Approver role
            if (scopes.Contains("isotopes:approve") || scopes.Contains("api"))
            {
                identity.AddClaim(new Claim(Claims.Role, "isotope:approver"));
            }

            // isotopes:audit grants the Auditor role
            if (scopes.Contains("isotopes:audit") || scopes.Contains("api"))
            {
                identity.AddClaim(new Claim(Claims.Role, "isotope:auditor"));
            }

            identity.SetScopes(request.GetScopes());
            identity.SetResources(await this.scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync().ConfigureAwait(false));
            identity.SetDestinations(GetDestinations);

            return this.SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            // Retrieve the claims principal stored in the authorization code/refresh token
            AuthenticateResult result = await this.HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            if (!result.Succeeded)
            {
                return this.Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                    }));
            }

            string? sessionIdStr = result.Principal.FindFirst("sid")?.Value
                ?? result.Principal.FindFirst("session_id")?.Value;

            if (sessionIdStr is not null && Guid.TryParse(sessionIdStr, out Guid sessionIdGuid))
            {
                ValidateSessionResult validateResult = await this.validateSessionQueryHandler.HandleAsync(
                    new ValidateSessionQuery(new SessionId(sessionIdGuid)));

                // Only reject if session was explicitly revoked or expired.
                // "Session not found" is allowed (can happen after DB reset in dev mode).
                if (!validateResult.IsValid &&
                    validateResult.Reason != "Session not found")
                {
                    return this.Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = validateResult.Reason ?? "Session invalid"
                        }));
                }
            }

            var identity = new ClaimsIdentity(result.Principal!.Claims,
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: Claims.Name,
                roleType: Claims.Role);

            identity.SetDestinations(GetDestinations);

            return this.SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    /// <summary>
    /// Handles the userinfo endpoint.
    /// </summary>
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    public async Task<IActionResult> UserInfo()
    {
        AuthenticateResult result = await this.HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            return this.Challenge(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The access token is not valid."
                }));
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = result.Principal!.GetClaim(Claims.Subject)!
        };

        if (result.Principal.GetClaim(Claims.Name) is { } name)
        {
            claims[Claims.Name] = name;
        }

        if (result.Principal.GetClaim(Claims.Email) is { } email)
        {
            claims[Claims.Email] = email;
        }

        return this.Ok(claims);
    }

    // NOTE: Logout endpoint is handled by LogoutController which implements
    // full Single Logout (SLO) with front-channel and back-channel support.

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // Custom destinations support
        if (claim.Properties.TryGetValue("destinations", out string? destinations))
        {
            if (!string.IsNullOrEmpty(destinations))
            {
                foreach (string dest in destinations.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    yield return dest;
                }
                yield break;
            }
        }

        // Note: by default, claims are NOT automatically included in the access and identity tokens.
        // To include them, you must set their destinations here.

        switch (claim.Type)
        {
            case Claims.Name or Claims.PreferredUsername:
                yield return Destinations.AccessToken;

                if (claim.Subject?.HasScope(Scopes.Profile) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;

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
            case "session_id":
                if (claim.Subject?.HasScope(Scopes.OpenId) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            // Never include the security stamp in the access and identity tokens, as it's a secret value
            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
