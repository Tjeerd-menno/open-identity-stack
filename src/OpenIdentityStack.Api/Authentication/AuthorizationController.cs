using System.Collections.Immutable;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using OpenIdentityStack.Application.Abstractions;
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
    private const string legacySessionIdClaim = "session_id";
    private const string requestedUserInfoClaim = "requested_userinfo_claim";
    private const string authenticationContextClassReferenceClaim = "acr";
    private const string supportedAcrValue = "1";

    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IOpenIddictScopeManager scopeManager;
    private readonly IUserRepository userRepository;
    private readonly IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler;
    private readonly IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler;
    private readonly IAddClientSessionUseCase addClientSessionUseCase;
    private readonly IValidateSessionQueryHandler validateSessionQueryHandler;
    private readonly IOpenIddictRequestService requestService;
    private readonly IHostEnvironment? environment;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        IUserRepository userRepository,
        IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler,
        IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler,
        IAddClientSessionUseCase addClientSessionUseCase,
        IValidateSessionQueryHandler validateSessionQueryHandler,
        IOpenIddictRequestService requestService,
        IHostEnvironment? environment = null)
    {
        this.applicationManager = applicationManager;
        this.scopeManager = scopeManager;
        this.userRepository = userRepository;
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

        AuthenticateResult cookieAuthentication = await this.HttpContext.AuthenticateAsync("Cookies");
        ClaimsPrincipal? authenticatedUser = cookieAuthentication is { Succeeded: true, Principal: { Identity.IsAuthenticated: true } }
            ? cookieAuthentication.Principal
            : this.User.Identity?.IsAuthenticated == true ? this.User : null;

        DateTimeOffset? authenticationTime = GetAuthenticationTime(cookieAuthentication.Properties, authenticatedUser);
        bool isAuthenticated = authenticatedUser?.Identity?.IsAuthenticated == true;

        // Check if prompt=login was requested - this forces re-authentication
        bool forceLogin = request.HasPromptValue("login");

        if (request.MaxAge is long maxAge)
        {
            forceLogin |= authenticationTime is null
                || DateTimeOffset.UtcNow - authenticationTime.Value > TimeSpan.FromSeconds(maxAge);
        }

        bool promptNone = request.HasPromptValue("none");

        if (promptNone && (forceLogin || !isAuthenticated))
        {
            return this.Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The user is not currently authenticated."
                }));
        }

        // If prompt=login is requested, sign out current session and force fresh login
        if (forceLogin && isAuthenticated)
        {
            await this.HttpContext.SignOutAsync("Cookies");
            await this.HttpContext.SignOutAsync("ExternalCookie");

            // Preserve the full authorization request in the return URL
            string returnUrl = this.Request.PathBase + this.Request.Path + QueryString.Create(
                this.Request.HasFormContentType ? this.Request.Form.ToList() : this.Request.Query.ToList());
            returnUrl = ConsumeFreshLoginParameters(returnUrl, request);

            // Pass fresh=true to indicate we need a fresh external login
            return this.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}&fresh=true");
        }

        // If the user is not authenticated, redirect to login page
        if (!isAuthenticated)
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

        ClaimsPrincipal user = authenticatedUser ?? throw new InvalidOperationException("The authenticated user cannot be resolved.");

        // Add the claims that will be persisted in the tokens
        string userIdString = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Subject claim not found.");
        identity.AddClaim(new System.Security.Claims.Claim(Claims.Subject, userIdString));

        UserId? userId = TryParseUserId(userIdString);
        Domain.Users.User? persistedUser = userId is { } parsedUserId
            ? await this.userRepository.GetByIdAsync(parsedUserId)
            : null;

        if (persistedUser?.DisplayName is { Length: > 0 } persistedDisplayName)
        {
            identity.AddClaim(new Claim(Claims.Name, persistedDisplayName));
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
            identity.AddClaim(new Claim(authenticationContextClassReferenceClaim, acr));
        }

        foreach (string requestedClaim in GetRequestedUserInfoClaims(request))
        {
            identity.AddClaim(new Claim(requestedUserInfoClaim, requestedClaim));
        }

        if (user.FindFirstValue("sid") is { } sessionIdStr && Guid.TryParse(sessionIdStr, out Guid sessionIdGuid))
        {
            var sessionId = new SessionId(sessionIdGuid);

            if (!string.IsNullOrEmpty(request.ClientId))
            {
                await this.addClientSessionUseCase.ExecuteAsync(new AddClientSessionCommand(
                    sessionId,
                    request.ClientId));
            }

            identity.AddClaim(new Claim("sid", sessionIdStr));
            identity.AddClaim(new Claim(legacySessionIdClaim, sessionIdStr));
        }
        else if (user.FindFirstValue(legacySessionIdClaim) is { } legacySessionIdStr && Guid.TryParse(legacySessionIdStr, out Guid legacySessionIdGuid))
        {
            var sessionId = new SessionId(legacySessionIdGuid);

            if (!string.IsNullOrEmpty(request.ClientId))
            {
                await this.addClientSessionUseCase.ExecuteAsync(new AddClientSessionCommand(
                    sessionId,
                    request.ClientId));
            }

            identity.AddClaim(new Claim("sid", legacySessionIdStr));
            identity.AddClaim(new Claim(legacySessionIdClaim, legacySessionIdStr));
        }

        // Add role claims (direct + group mapped)
        if (userId is { } resolvedUserId)
        {
            // 1. Roles and Permissions
            Result<IReadOnlyList<RoleDto>> rolesResult = await this.getUserEffectiveRolesQueryHandler.HandleAsync(resolvedUserId);
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
            Result<IReadOnlyList<GroupClaimDto>> groupClaimsResult = await this.getGroupClaimsForUserQueryHandler.HandleAsync(resolvedUserId);
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

        return this.SignIn(
            new ClaimsPrincipal(identity),
            CreateOpenIddictAuthenticationProperties(authenticationTime),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
                ?? result.Principal.FindFirst(legacySessionIdClaim)?.Value;

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

            var identity = new ClaimsIdentity(result.Principal!.Claims.Where(claim =>
                    !string.Equals(claim.Type, legacySessionIdClaim, StringComparison.Ordinal)
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
            return this.SignIn(
                new ClaimsPrincipal(identity),
                CreateOpenIddictAuthenticationProperties(authenticationTime),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    /// <summary>
    /// Handles the token introspection endpoint.
    /// </summary>
    [HttpPost("~/connect/introspect")]
    [EnableRateLimiting("IntrospectionEndpoint")]
    public async Task<IActionResult> Introspect()
    {
        OpenIddictRequest request = this.requestService.GetRequest(this.HttpContext) ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        AuthenticateResult result = await this.HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (!result.Succeeded || result.Principal is null)
        {
            return this.Challenge(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The access token is not valid."
                }));
        }

        string? subject = result.Principal.GetClaim(Claims.Subject);
        IReadOnlyList<string> permissions = await this.ResolveIntrospectionPermissionsAsync(
            result.Principal,
            subject,
            request.ClientId,
            this.HttpContext.RequestAborted);

        var response = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["active"] = true,
            ["permissions"] = permissions
        };

        if (!string.IsNullOrWhiteSpace(subject))
        {
            response[Claims.Subject] = subject;
        }

        return this.Ok(response);
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

        ImmutableHashSet<string> requestedUserInfoClaims = GetRequestedUserInfoClaims(result.Principal);

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

        if (result.Principal.HasScope(Scopes.Email)
            || requestedUserInfoClaims.Contains(Claims.EmailVerified))
        {
            if (result.Principal.GetClaim(Claims.EmailVerified) is { } emailVerified)
            {
                claims[Claims.EmailVerified] = string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        return this.Ok(claims);
    }

    // NOTE: Logout endpoint is handled by LogoutController which implements
    // full Single Logout (SLO) with front-channel and back-channel support.

    private async Task<IReadOnlyList<string>> ResolveIntrospectionPermissionsAsync(
        ClaimsPrincipal principal,
        string? subject,
        string? requestingClientId,
        CancellationToken cancellationToken)
    {
        var permissions = new List<string>();
        bool resolvedFromFreshRoles = false;

        if (Guid.TryParse(subject, out Guid userId))
        {
            Result<IReadOnlyList<RoleDto>> rolesResult =
                await this.getUserEffectiveRolesQueryHandler.HandleAsync(new UserId(userId), cancellationToken);

            if (rolesResult.IsSuccess)
            {
                resolvedFromFreshRoles = true;
                foreach (RoleDto role in rolesResult.Value)
                {
                    permissions.AddRange(role.Permissions);
                }
            }
        }

        if (!resolvedFromFreshRoles)
        {
            permissions.AddRange(GetPermissionClaims(principal));
        }

        return FilterPermissionsForCaller(permissions, requestingClientId);
    }

    private static List<string> FilterPermissionsForCaller(
        IEnumerable<string> permissions,
        string? requestingClientId)
    {
        if (string.IsNullOrWhiteSpace(requestingClientId))
        {
            return [];
        }

        var filtered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string permission in permissions)
        {
            if (!IsPermissionRelevantToCaller(permission, requestingClientId)
                || !seen.Add(permission))
            {
                continue;
            }

            filtered.Add(permission);
        }

        return filtered;
    }

    private static bool IsPermissionRelevantToCaller(string permission, string requestingClientId) =>
        string.Equals(permission, OpenIdentityStack.Application.Authorization.Permissions.All, StringComparison.Ordinal)
        || permission.StartsWith($"{requestingClientId}:", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> GetPermissionClaims(ClaimsPrincipal principal) =>
        principal.FindAll("permission")
            .Concat(principal.FindAll("permissions"))
            .SelectMany(static claim => claim.Value.Split(
                [' ', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        if (IsInternalTokenStateClaim(claim.Type))
        {
            yield break;
        }

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
            case requestedUserInfoClaim:
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
                    || claim.Subject?.HasClaim(requestedUserInfoClaim, claim.Type) == true)
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
                    || claim.Subject?.HasClaim(requestedUserInfoClaim, claim.Type) == true)
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

            case authenticationContextClassReferenceClaim:
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

    private static bool IsInternalTokenStateClaim(string claimType) =>
        claimType is legacySessionIdClaim or "oi_au_id" or "oi_tkn_id"
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

    private static ImmutableHashSet<string> GetRequestedUserInfoClaims(ClaimsPrincipal principal) =>
        principal.FindAll(requestedUserInfoClaim)
            .Select(claim => claim.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToImmutableHashSet(StringComparer.Ordinal);

    private static UserId? TryParseUserId(string rawUserId) =>
        Guid.TryParse(rawUserId, out Guid userId) ? new UserId(userId) : null;

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
        Dictionary<string, object> claims,
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
        Dictionary<string, object> claims,
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
            .FirstOrDefault(value => string.Equals(value, supportedAcrValue, StringComparison.Ordinal));
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
            .Where(static name => !string.IsNullOrWhiteSpace(name))
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

    private static DateTimeOffset? GetAuthenticationTime(AuthenticationProperties? properties, ClaimsPrincipal? principal)
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

        return TryParseUnixTime(rawValue);
    }

    private static DateTimeOffset? TryParseUnixTime(string? rawValue) =>
        long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : null;

    private static void SetAuthenticationTimeClaim(ClaimsIdentity identity, DateTimeOffset authenticationTime)
    {
        foreach (Claim claim in identity.FindAll(Claims.AuthenticationTime).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(
            Claims.AuthenticationTime,
            authenticationTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));
    }

    private static AuthenticationProperties CreateOpenIddictAuthenticationProperties(DateTimeOffset? authenticationTime)
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
                .SelectMany(value => value?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
                .Where(value => !string.Equals(value, "login", StringComparison.Ordinal))
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
            .SelectMany(parameter => parameter.Value, (parameter, value) => new KeyValuePair<string, string?>(parameter.Key, value));

        return path + QueryString.Create(queryParameters);
    }
}
