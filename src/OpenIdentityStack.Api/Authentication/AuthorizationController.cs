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
using OpenIdentityStack.Application.Resources;

using SharedKernel;
namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Controller handling OpenIddict authorization and token endpoints.
/// </summary>
[ApiController]
public class AuthorizationController : ControllerBase
{
    private const string legacySessionIdClaim = "session_id";
    private const string supportedAcrValue = "1";

    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IUserRepository userRepository;
    private readonly IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler;
    private readonly IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler;
    private readonly IAddClientSessionUseCase addClientSessionUseCase;
    private readonly IValidateSessionQueryHandler validateSessionQueryHandler;
    private readonly IOpenIddictRequestService requestService;
    private readonly IAuditLog auditLog;
    private readonly ITokenClaimProjectionService tokenClaimProjectionService;
    private readonly IResourcePermissionService? resourcePermissionService;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        IUserRepository userRepository,
        IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler,
        IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler,
        IAddClientSessionUseCase addClientSessionUseCase,
        IValidateSessionQueryHandler validateSessionQueryHandler,
        IOpenIddictRequestService requestService,
        IAuditLog auditLog,
        IApplicationPermissionRegistryRepository? applicationPermissionRegistryRepository = null,
        IHostEnvironment? environment = null,
        IPermissionClaimProjectionService? permissionClaimProjectionService = null,
        ITokenClaimProjectionService? tokenClaimProjectionService = null,
        IResourcePermissionService? resourcePermissionService = null)
    {
        this.applicationManager = applicationManager;
        this.userRepository = userRepository;
        this.getUserEffectiveRolesQueryHandler = getUserEffectiveRolesQueryHandler;
        this.getGroupClaimsForUserQueryHandler = getGroupClaimsForUserQueryHandler;
        this.addClientSessionUseCase = addClientSessionUseCase;
        this.validateSessionQueryHandler = validateSessionQueryHandler;
        this.requestService = requestService;
        this.auditLog = auditLog;
        this.tokenClaimProjectionService = tokenClaimProjectionService ?? new TokenClaimProjectionService();
        this.resourcePermissionService = resourcePermissionService;
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

        // Consume freshness once even when the existing cookie has already become anonymous.
        if (forceLogin)
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

        ClaimsPrincipal user = authenticatedUser ?? throw new InvalidOperationException("The authenticated user cannot be resolved.");
        string userIdString = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Subject claim not found.");

        UserId? userId = TryParseUserId(userIdString);
        Domain.Users.User? persistedUser = userId is { } parsedUserId
            ? await this.userRepository.GetByIdAsync(parsedUserId)
            : null;

        if (persistedUser?.Status == Domain.Users.UserStatus.Disabled)
        {
            await this.AuditDisabledAccountAsync(persistedUser.Id, "authorization");
            return this.RejectUnavailableCredentials(Errors.AccessDenied);
        }

        string? sessionIdValue = user.FindFirstValue("sid") ?? user.FindFirstValue(legacySessionIdClaim);
        if (!Guid.TryParse(sessionIdValue, out Guid sessionIdGuid)
            || !(await this.validateSessionQueryHandler.HandleAsync(
                new ValidateSessionQuery(new SessionId(sessionIdGuid)), this.HttpContext.RequestAborted)).IsValid)
        {
            await this.HttpContext.SignOutAsync("Cookies");
            if (promptNone)
            {
                return this.RejectUnavailableCredentials(Errors.LoginRequired);
            }

            string returnUrl = this.Request.PathBase + this.Request.Path + QueryString.Create(
                this.Request.HasFormContentType ? this.Request.Form.ToList() : this.Request.Query.ToList());
            return this.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}&fresh=true");
        }

        if (!string.IsNullOrEmpty(request.ClientId))
        {
            await this.addClientSessionUseCase.ExecuteAsync(new AddClientSessionCommand(
                new SessionId(sessionIdGuid), request.ClientId), this.HttpContext.RequestAborted);
        }

        var roleNames = new List<string>();

        IReadOnlyList<GroupClaimDto> groupClaims = [];

        // Add role claims (direct + group mapped)
        if (userId is { } resolvedUserId)
        {
            // 1. Roles and Permissions
            Result<IReadOnlyList<RoleDto>> rolesResult = await this.getUserEffectiveRolesQueryHandler.HandleAsync(resolvedUserId);
             if (rolesResult.IsSuccess)
             {
                 foreach (RoleDto role in rolesResult.Value)
                 {
                     roleNames.Add(role.Name);

                 }
             }

            // 2. Group Claims
            Result<IReadOnlyList<GroupClaimDto>> groupClaimsResult = await this.getGroupClaimsForUserQueryHandler.HandleAsync(resolvedUserId);
             if (groupClaimsResult.IsSuccess)
             {
                 groupClaims = groupClaimsResult.Value;
             }
        }

        Result<ResourceTokenProjection> resourceAccess = await this.ProjectResourcesAsync(request, request.GetScopes(), userId);
        if (resourceAccess.IsFailure) { return this.ResourceAccessDenied(resourceAccess.Error, isAuthorizationRequest: true); }

        ClaimsPrincipal projectedPrincipal = this.tokenClaimProjectionService.ProjectSubjectClaims(
            new TokenClaimProjectionRequest(
                user,
                persistedUser,
                roleNames,
                resourceAccess.Value.Permissions,
                groupClaims,
                request.GetScopes(),
                GetRequestedUserInfoClaims(request),
                authenticationTime,
                GetSupportedAcrValue(request),
                sessionIdValue));
        ApplyResourceAccess(projectedPrincipal, request.ClientId!, ResourceTokenActorTypes.User, resourceAccess.Value);

        return this.SignIn(
            projectedPrincipal,
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
            identity.AddClaim(new Claim(TokenSubjectClaims.Kind, TokenSubjectClaims.Application).SetDestinations(Destinations.AccessToken));
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, clientId!));

            string? displayName = await this.applicationManager.GetDisplayNameAsync(application);
            if (!string.IsNullOrEmpty(displayName))
            {
                identity.AddClaim(new Claim(Claims.Name, displayName));
            }

            Result<ResourceTokenProjection> resourceAccess = await this.ProjectResourcesAsync(request, request.GetScopes(), null);
            if (resourceAccess.IsFailure) { return this.ResourceAccessDenied(resourceAccess.Error); }
            identity.SetScopes(request.GetScopes());
            var principal = new ClaimsPrincipal(identity);
            ApplyResourceAccess(principal, request.ClientId!, ResourceTokenActorTypes.Application, resourceAccess.Value);
            identity.SetDestinations(TokenClaimProjectionService.GetDestinations);

            return this.SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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

            string? subject = result.Principal.GetClaim(Claims.Subject) ?? result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            Domain.Users.User? tokenUser = null;
            if (TryParseUserId(subject ?? string.Empty) is { } tokenUserId)
            {
                tokenUser = await this.userRepository.GetByIdAsync(tokenUserId, this.HttpContext.RequestAborted);
                if (tokenUser?.Status == Domain.Users.UserStatus.Disabled)
                {
                    await this.AuditDisabledAccountAsync(tokenUser.Id, request.IsAuthorizationCodeGrantType() ? GrantTypes.AuthorizationCode : GrantTypes.RefreshToken);
                    return this.RejectUnavailableCredentials();
                }
            }

            string? sessionIdStr = result.Principal.FindFirst("sid")?.Value
                ?? result.Principal.FindFirst(legacySessionIdClaim)?.Value;

            if (sessionIdStr is not null && Guid.TryParse(sessionIdStr, out Guid sessionIdGuid))
            {
                ValidateSessionResult validateResult = await this.validateSessionQueryHandler.HandleAsync(
                    new ValidateSessionQuery(new SessionId(sessionIdGuid)));

                if (!validateResult.IsValid)
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

            DateTimeOffset? authenticationTime = GetAuthenticationTime(result.Properties, result.Principal!);
            ImmutableArray<string> scopes = request.GetScopes().IsEmpty ? result.Principal!.GetScopes() : request.GetScopes();
            if (scopes.Any(scope => !result.Principal!.HasScope(scope)))
            {
                return this.ResourceAccessDenied(Domain.Resources.ResourceAccessErrors.NotGranted);
            }
            UserId? subjectId = TryParseUserId(result.Principal!.GetClaim(Claims.Subject) ?? string.Empty);
            if (subjectId is null) { return this.ResourceAccessDenied(Domain.Resources.ResourceAccessErrors.NotGranted); }
            Result<ResourceTokenProjection> resourceAccess = await this.ProjectResourcesAsync(request, scopes, subjectId,
                result.Principal.FindAll("permission").Select(static claim => claim.Value).ToArray(), result.Principal.GetResources());
            if (resourceAccess.IsFailure) { return this.ResourceAccessDenied(resourceAccess.Error); }
            ClaimsPrincipal projectedPrincipal = this.tokenClaimProjectionService.ProjectExistingPrincipal(result.Principal!, authenticationTime, tokenUser);
            projectedPrincipal.SetScopes(scopes);
            ApplyResourceAccess(projectedPrincipal, request.ClientId!, ResourceTokenActorTypes.User, resourceAccess.Value);
            return this.SignIn(
                projectedPrincipal,
                CreateOpenIddictAuthenticationProperties(authenticationTime),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
            return this.InvalidUserInfoToken();
        }

        ClaimsPrincipal principal = result.Principal!;
        string? subject = principal.GetClaim(Claims.Subject);
        string? revision = principal.GetClaim(UserCredentialClaims.Revision);
        Claim[] subjectKinds = principal.FindAll(TokenSubjectClaims.Kind).ToArray();
        bool validApplicationSubject = subjectKinds is [{ Value: TokenSubjectClaims.Application }]
            && revision is null
            && !string.IsNullOrWhiteSpace(subject)
            && string.Equals(subject, principal.GetClaim(Claims.ClientId), StringComparison.Ordinal);
        if (subjectKinds.Length != 0 && !validApplicationSubject)
        {
            return this.InvalidUserInfoToken();
        }

        bool ambiguousLegacyApplicationSubject = subjectKinds.Length == 0
            && revision is null
            && !string.IsNullOrWhiteSpace(subject)
            && string.Equals(subject, principal.GetClaim(Claims.ClientId), StringComparison.Ordinal)
            && await this.applicationManager.FindByClientIdAsync(subject, this.HttpContext.RequestAborted) is not null;
        if (ambiguousLegacyApplicationSubject)
        {
            return this.InvalidUserInfoToken();
        }

        Domain.Users.User? emailEvidenceUser = null;
        if (!validApplicationSubject)
        {
            if (TryParseUserId(subject ?? string.Empty) is not { } userId)
            {
                return this.InvalidUserInfoToken();
            }

            emailEvidenceUser = await this.userRepository.GetByIdAsync(userId, this.HttpContext.RequestAborted);
        }

        ClaimsPrincipal projected = this.tokenClaimProjectionService.ProjectExistingPrincipal(result.Principal!, persistedUser: emailEvidenceUser);
        return this.Ok(this.tokenClaimProjectionService.CreateUserInfoResponse(projected));
    }

    private ChallengeResult InvalidUserInfoToken() => this.Challenge(
        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        properties: new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The access token is not valid."
        }));

    // NOTE: Logout endpoint is handled by LogoutController which implements
    // full Single Logout (SLO) with front-channel and back-channel support.

    private Task AuditDisabledAccountAsync(UserId userId, string flow) =>
        this.auditLog.LogAsync(userId.Value.ToString(), "Authentication.DisabledAccountDenied", "User", userId.Value.ToString(),
            $"Local account is disabled. Flow: {flow}.", this.HttpContext.RequestAborted);

    private ForbidResult RejectUnavailableCredentials(string error = Errors.InvalidGrant) => this.Forbid(
        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        properties: new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The credentials are no longer valid."
        }));

    private Task<Result<ResourceTokenProjection>> ProjectResourcesAsync(OpenIddictRequest request, IReadOnlyList<string> scopes, UserId? userId,
        IReadOnlyList<string>? originalPermissions = null, IReadOnlyList<string>? originalAudiences = null) =>
        this.resourcePermissionService is null
            ? Task.FromResult<Result<ResourceTokenProjection>>(Domain.Resources.ResourceAccessErrors.NotGranted)
            : this.resourcePermissionService.ProjectAsync(new ResourceTokenRequest(request.ClientId ?? string.Empty, scopes,
                request.GetResources(), userId, originalPermissions, originalAudiences), this.HttpContext.RequestAborted);

    private ForbidResult ResourceAccessDenied(DomainError error, bool isAuthorizationRequest = false) => this.Forbid(
        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        properties: new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error == Domain.Resources.ResourceAccessErrors.UnknownResource
                ? "invalid_target" : isAuthorizationRequest ? Errors.AccessDenied : Errors.InvalidGrant,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Access to the requested resource is unavailable."
        }));

    private static void ApplyResourceAccess(ClaimsPrincipal principal, string clientId, string actorType, ResourceTokenProjection access)
    {
        foreach (ClaimsIdentity identity in principal.Identities)
        {
            foreach (Claim claim in identity.Claims.Where(static claim => claim.Type is "permission" or "permissions" or "client_id" or "ois.grant_revision" or ResourceTokenActorTypes.ClaimType).ToArray())
            {
                identity.RemoveClaim(claim);
            }
        }
        var target = (ClaimsIdentity)principal.Identity!;
        target.AddClaim(new Claim(Claims.ClientId, clientId).SetDestinations(Destinations.AccessToken));
        target.AddClaim(new Claim(ResourceTokenActorTypes.ClaimType, actorType).SetDestinations(Destinations.AccessToken));
        foreach (string permission in access.Permissions) { target.AddClaim(new Claim("permission", permission).SetDestinations(Destinations.AccessToken)); }
        foreach ((Guid resourceId, long revision) in access.GrantRevisions)
        {
            target.AddClaim(new Claim("ois.grant_revision", $"{resourceId:D}:{revision}").SetDestinations(Destinations.AccessToken));
        }
        principal.SetResources(access.Audiences);
        // Resource-sensitive proof destinations must use the final granted audience, including on refresh.
        principal.SetDestinations(TokenClaimProjectionService.GetDestinations);
        principal.SetPresenters(clientId);
    }

    private static ImmutableHashSet<string> GetRequestedUserInfoClaims(OpenIddictRequest request)
    {
        if (!request.TryGetParameter(Parameters.Claims, out OpenIddictParameter parameter)
            || OpenIddictParameter.IsNullOrEmpty(parameter))
        {
            return ImmutableHashSet<string>.Empty;
        }

        return ParseRequestedClaims(parameter, "userinfo");
    }

    private static UserId? TryParseUserId(string rawUserId) =>
        Guid.TryParse(rawUserId, out Guid userId) ? new UserId(userId) : null;

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
