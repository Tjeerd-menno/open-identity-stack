using System.Collections.Immutable;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
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
public class AuthorizationController : Controller
{
    private const string legacySessionIdClaim = "session_id";
    private const string supportedAcrValue = "1";
    private const string consentPurpose = "OpenIdentityStack.AuthorizationConsent.v1";
    private const string consentActionField = "consent_action";
    private const string consentTicketField = "consent_ticket";
    private const string consentAuthorizationIdItemKey = "OpenIdentityStack.ConsentAuthorizationId";
    private const string consentedClaimsProperty = "OpenIdentityStack.ConsentedClaims";

    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IOpenIddictAuthorizationManager authorizationManager;
    private readonly IConsentApprovalTransactionRunner consentApprovalTransactionRunner;
    private readonly IUserRepository userRepository;
    private readonly IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler;
    private readonly IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler;
    private readonly IAddClientSessionUseCase addClientSessionUseCase;
    private readonly IValidateSessionQueryHandler validateSessionQueryHandler;
    private readonly IOpenIddictRequestService requestService;
    private readonly IAuditLog auditLog;
    private readonly ITokenClaimProjectionService tokenClaimProjectionService;
    private readonly IResourcePermissionService? resourcePermissionService;
    private readonly ICredentialSessionValidator credentialSessionValidator;
    private readonly IAntiforgery antiforgery;
    private readonly IDataProtector consentProtector;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IConsentApprovalTransactionRunner consentApprovalTransactionRunner,
        IOpenIddictScopeManager scopeManager,
        IUserRepository userRepository,
        IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler,
        IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler,
        IAddClientSessionUseCase addClientSessionUseCase,
        IValidateSessionQueryHandler validateSessionQueryHandler,
        IOpenIddictRequestService requestService,
        IAuditLog auditLog,
        ICredentialSessionValidator credentialSessionValidator,
        IAntiforgery antiforgery,
        IDataProtectionProvider dataProtection,
        IApplicationPermissionRegistryRepository? applicationPermissionRegistryRepository = null,
        IHostEnvironment? environment = null,
        ITokenClaimProjectionService? tokenClaimProjectionService = null,
        IResourcePermissionService? resourcePermissionService = null)
    {
        this.applicationManager = applicationManager;
        this.authorizationManager = authorizationManager;
        this.consentApprovalTransactionRunner = consentApprovalTransactionRunner;
        this.userRepository = userRepository;
        this.getUserEffectiveRolesQueryHandler = getUserEffectiveRolesQueryHandler;
        this.getGroupClaimsForUserQueryHandler = getGroupClaimsForUserQueryHandler;
        this.addClientSessionUseCase = addClientSessionUseCase;
        this.validateSessionQueryHandler = validateSessionQueryHandler;
        this.requestService = requestService;
        this.auditLog = auditLog;
        this.credentialSessionValidator = credentialSessionValidator;
        this.antiforgery = antiforgery;
        this.consentProtector = dataProtection.CreateProtector(consentPurpose);
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
            || userId is null
            || !await this.HasValidCredentialSessionAsync(user, userId.Value)
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

        if (!string.IsNullOrEmpty(request.ClientId)
            && await this.applicationManager.FindByClientIdAsync(request.ClientId, this.HttpContext.RequestAborted) is { } client
            && string.Equals(await this.applicationManager.GetConsentTypeAsync(client, this.HttpContext.RequestAborted),
                ConsentTypes.Explicit, StringComparison.Ordinal))
        {
            IActionResult? consent = await this.HandleExplicitConsentAsync(
                request, client, userIdString, sessionIdGuid, promptNone);
            if (consent is not null) { return consent; }
        }

        if (!string.IsNullOrEmpty(request.ClientId))
        {
            Result participation = await this.addClientSessionUseCase.ExecuteAsync(new AddClientSessionCommand(
                new SessionId(sessionIdGuid), request.ClientId), this.HttpContext.RequestAborted);
            if (participation.IsFailure)
            {
                return this.RejectUnavailableCredentials(Errors.AccessDenied);
            }
        }

        var roleNames = new List<string>();
        IReadOnlyList<string>? userPermissions = null;

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

                 // Hand the already-resolved permissions to resource projection so it does not
                 // re-run the effective-roles queries for the same user in this request.
                 userPermissions = rolesResult.Value
                     .Where(static role => role.IsActive)
                     .SelectMany(static role => role.Permissions)
                     .ToArray();
             }

            // 2. Group Claims
            Result<IReadOnlyList<GroupClaimDto>> groupClaimsResult = await this.getGroupClaimsForUserQueryHandler.HandleAsync(resolvedUserId);
             if (groupClaimsResult.IsSuccess)
             {
                 groupClaims = groupClaimsResult.Value;
             }
        }

        Result<ResourceTokenProjection> resourceAccess = await this.ProjectResourcesAsync(request, request.GetScopes(), userId, userPermissions: userPermissions);
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
        if (this.HttpContext.Items.TryGetValue(consentAuthorizationIdItemKey, out object? authorizationId)
            && authorizationId is string consentAuthorizationId)
        {
            projectedPrincipal.SetAuthorizationId(consentAuthorizationId);
        }

        return this.SignIn(
            projectedPrincipal,
            CreateOpenIddictAuthenticationProperties(authenticationTime),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult?> HandleExplicitConsentAsync(
        OpenIddictRequest request,
        object client,
        string subject,
        Guid sessionId,
        bool promptNone)
    {
        string? existingAuthorizationId = request.HasPromptValue("consent")
            ? null
            : await this.FindExistingConsentAuthorizationIdAsync(
                request, client, subject, this.HttpContext.RequestAborted);
        if (existingAuthorizationId is not null)
        {
            this.HttpContext.Items[consentAuthorizationIdItemKey] = existingAuthorizationId;
            return null;
        }

        if (promptNone)
        {
            return this.RejectConsent(Errors.ConsentRequired);
        }

        IFormCollection? form = this.Request.HasFormContentType ? await this.Request.ReadFormAsync() : null;
        bool submitted = form is not null
            && (form.ContainsKey(consentActionField) || form.ContainsKey(consentTicketField));
        if (submitted)
        {
            try
            {
                await this.antiforgery.ValidateRequestAsync(this.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return this.BadRequest();
            }

            if (!TryReadConsentTicket(form![consentTicketField].ToString(), out ConsentTicket? ticket)
                || ticket is null
                || ticket.ExpiresUtc <= DateTimeOffset.UtcNow
                || ticket.Subject != subject
                || ticket.SessionId != sessionId
                || ticket.ClientId != request.ClientId
                || ticket.RequestFingerprint != GetConsentRequestFingerprint(request))
            {
                return this.RejectConsent(Errors.AccessDenied);
            }

            string decision = form[consentActionField].ToString();
            if (decision == "deny")
            {
                await this.auditLog.LogAsync(subject, "Consent.Denied", "Application",
                    request.ClientId!, $"Subject {subject} denied consent.", this.HttpContext.RequestAborted);
                return this.RejectConsent(Errors.AccessDenied);
            }
            if (decision != "approve")
            {
                return this.RejectConsent(Errors.AccessDenied);
            }

            string authorizationId = await this.consentApprovalTransactionRunner.ExecuteAsync(async cancellationToken =>
            {
                string createdAuthorizationId = await this.CreateConsentAuthorizationAsync(
                    request, client, subject, cancellationToken);
                await this.auditLog.LogAsync(subject, "Consent.Approved", "Application",
                    request.ClientId!, $"Subject {subject} approved consent.", cancellationToken);
                return createdAuthorizationId;
            }, this.HttpContext.RequestAborted);
            this.HttpContext.Items[consentAuthorizationIdItemKey] = authorizationId;
            return null;
        }

        string ticketValue = this.consentProtector.Protect(JsonSerializer.Serialize(new ConsentTicket(
            subject, sessionId, request.ClientId!, GetConsentRequestFingerprint(request),
            DateTimeOffset.UtcNow.AddMinutes(5))));
        string clientName = await this.applicationManager.GetDisplayNameAsync(client, this.HttpContext.RequestAborted)
            ?? request.ClientId!;
        var parameters = new List<KeyValuePair<string, string>>();
        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> item in this.Request.Query)
        {
            if (item.Key is consentActionField or consentTicketField or "__RequestVerificationToken") { continue; }
            foreach (string? value in item.Value)
            {
                parameters.Add(new KeyValuePair<string, string>(item.Key, value ?? string.Empty));
            }
        }
        if (form is not null)
        {
            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> item in form)
            {
                if (item.Key is consentActionField or consentTicketField or "__RequestVerificationToken") { continue; }
                foreach (string? value in item.Value)
                {
                    parameters.Add(new KeyValuePair<string, string>(item.Key, value ?? string.Empty));
                }
            }
        }
        return this.View("~/Authentication/Views/Consent.cshtml", new ConsentViewModel(
            $"{this.Request.PathBase}/connect/authorize", clientName,
            request.GetScopes().ToArray(), GetRequestedConsentClaims(request),
            parameters, ticketValue));
    }

    private async Task<string?> FindExistingConsentAuthorizationIdAsync(
        OpenIddictRequest request,
        object client,
        string subject,
        CancellationToken cancellationToken)
    {
        string? applicationId = await this.applicationManager.GetIdAsync(client, cancellationToken);
        if (string.IsNullOrWhiteSpace(applicationId)) { return null; }

        IAsyncEnumerable<object> authorizations = this.authorizationManager.FindAsync(
            subject,
            applicationId,
            Statuses.Valid,
            AuthorizationTypes.Permanent,
            request.GetScopes(),
            cancellationToken);
        if (authorizations is null) { return null; }

        string[] requestedClaims = GetRequestedConsentClaims(request);
        await foreach (object authorization in authorizations.WithCancellation(cancellationToken))
        {
            System.Collections.Immutable.ImmutableDictionary<string, JsonElement> properties = await this.authorizationManager
                .GetPropertiesAsync(authorization, cancellationToken);
            if (!properties.TryGetValue(consentedClaimsProperty, out JsonElement consentedClaims)
                || consentedClaims.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var claimSet = consentedClaims.EnumerateArray()
                .Where(static claim => claim.ValueKind == JsonValueKind.String)
                .Select(static claim => claim.GetString()!)
                .ToHashSet(StringComparer.Ordinal);
            if (!requestedClaims.All(claimSet.Contains)) { continue; }

            return await this.authorizationManager.GetIdAsync(authorization, cancellationToken);
        }

        return null;
    }

    private async Task<string> CreateConsentAuthorizationAsync(
        OpenIddictRequest request,
        object client,
        string subject,
        CancellationToken cancellationToken)
    {
        string? applicationId = await this.applicationManager.GetIdAsync(client, cancellationToken);
        if (string.IsNullOrWhiteSpace(applicationId))
        {
            throw new InvalidOperationException("The OpenIddict application identifier cannot be resolved for consent persistence.");
        }

        OpenIddictAuthorizationDescriptor descriptor = new()
        {
            ApplicationId = applicationId,
            Subject = subject,
            Status = Statuses.Valid,
            Type = AuthorizationTypes.Permanent
        };
        descriptor.Scopes.UnionWith(request.GetScopes());
        using var claimsDocument = JsonDocument.Parse(JsonSerializer.Serialize(GetRequestedConsentClaims(request)));
        descriptor.Properties[consentedClaimsProperty] = claimsDocument.RootElement.Clone();

        object authorization = await this.authorizationManager.CreateAsync(descriptor, cancellationToken);
        string? authorizationId = await this.authorizationManager.GetIdAsync(authorization, cancellationToken);
        return authorizationId ?? throw new InvalidOperationException("The consent authorization identifier cannot be resolved.");
    }

    private ForbidResult RejectConsent(string error) => this.Forbid(
        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        properties: new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                "The user has not consented to this request."
        }));

    private bool TryReadConsentTicket(string protectedValue, out ConsentTicket? ticket)
    {
        ticket = null;
        if (string.IsNullOrWhiteSpace(protectedValue)) { return false; }
        try
        {
            ticket = JsonSerializer.Deserialize<ConsentTicket>(this.consentProtector.Unprotect(protectedValue));
            return ticket is not null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return false;
        }
    }

    private static string GetConsentRequestFingerprint(OpenIddictRequest request)
    {
        string canonical = JsonSerializer.Serialize(request.GetParameters()
            .Where(static item => item.Key is not consentActionField and not consentTicketField and not "__RequestVerificationToken")
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(static item => new KeyValuePair<string, string>(item.Key, item.Value.ToString() ?? string.Empty)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string[] GetRequestedConsentClaims(OpenIddictRequest request)
    {
        if (!request.TryGetParameter(Parameters.Claims, out OpenIddictParameter parameter)) { return []; }
        try
        {
            using var claims = JsonDocument.Parse(parameter.ToString() ?? string.Empty);
            if (claims.RootElement.ValueKind != JsonValueKind.Object) { return []; }
            return claims.RootElement.EnumerateObject()
                .Where(static item => item.Value.ValueKind == JsonValueKind.Object)
                .SelectMany(static item => item.Value.EnumerateObject().Select(claim => claim.Name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record ConsentTicket(
        string Subject, Guid SessionId, string ClientId, string RequestFingerprint, DateTimeOffset ExpiresUtc);

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
            identity.AddClaim(new Claim("token_kind", "client_credentials"));
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

            if (!Guid.TryParse(sessionIdStr, out Guid sessionIdGuid)
                || TryParseUserId(subject ?? string.Empty) is not { } sessionUserId
                || !await this.HasValidCredentialSessionAsync(result.Principal, sessionUserId))
            {
                return this.RejectUnavailableCredentials();
            }

            {
                ValidateSessionResult validateResult = await this.validateSessionQueryHandler.HandleAsync(
                    new ValidateSessionQuery(new SessionId(sessionIdGuid)));

                if (!validateResult.IsValid)
                {
                    return this.Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
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
            Result<IReadOnlyList<RoleDto>> roles = await this.getUserEffectiveRolesQueryHandler.HandleAsync(subjectId.Value, this.HttpContext.RequestAborted);
            Result<IReadOnlyList<GroupClaimDto>> groups = await this.getGroupClaimsForUserQueryHandler.HandleAsync(subjectId.Value, this.HttpContext.RequestAborted);
            if (roles.IsFailure || groups.IsFailure || tokenUser is null)
            {
                return this.RejectUnavailableCredentials();
            }
            ClaimsPrincipal projectedPrincipal = this.tokenClaimProjectionService.ProjectSubjectClaims(new TokenClaimProjectionRequest(
                result.Principal!, tokenUser, roles.Value.Where(static role => role.IsActive).Select(static role => role.Name).ToArray(),
                resourceAccess.Value.Permissions, groups.Value, scopes,
                result.Principal!.FindAll(TokenClaimProjectionService.RequestedUserInfoClaim).Select(static claim => claim.Value).ToImmutableHashSet(StringComparer.Ordinal),
                authenticationTime, result.Principal.GetClaim(TokenClaimProjectionService.AuthenticationContextClassReferenceClaim), sessionIdStr));
            ApplyResourceAccess(projectedPrincipal, request.ClientId!, ResourceTokenActorTypes.User, resourceAccess.Value);
            if (result.Principal.GetAuthorizationId() is { Length: > 0 } authorizationId)
            {
                projectedPrincipal.SetAuthorizationId(authorizationId);
            }

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
        bool applicationSubject = subjectKinds is [{ Value: TokenSubjectClaims.Application }]
            && revision is null
            && !string.IsNullOrWhiteSpace(subject)
            && string.Equals(subject, principal.GetClaim(Claims.ClientId), StringComparison.Ordinal);
        if (subjectKinds.Length != 0 && !applicationSubject)
        {
            return this.InvalidUserInfoToken();
        }

        bool legacyApplicationSubject = subjectKinds.Length == 0
            && revision is null
            && !string.IsNullOrWhiteSpace(subject)
            && string.Equals(subject, principal.GetClaim(Claims.ClientId), StringComparison.Ordinal)
            && await this.applicationManager.FindByClientIdAsync(subject, this.HttpContext.RequestAborted) is not null;
        if (legacyApplicationSubject)
        {
            if (TryParseUserId(subject!) is { } possibleUserId
                && await this.userRepository.GetByIdAsync(possibleUserId, this.HttpContext.RequestAborted) is not null)
            {
                return this.InvalidUserInfoToken();
            }

            applicationSubject = true;
        }

        Domain.Users.User? emailEvidenceUser = null;
        if (!applicationSubject)
        {
            if (TryParseUserId(subject ?? string.Empty) is not { } userId
                || !await this.HasValidCredentialSessionAsync(principal, userId))
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

    private async Task<bool> HasValidCredentialSessionAsync(ClaimsPrincipal principal, UserId userId)
    {
        string? sessionId = principal.FindFirstValue("sid") ?? principal.FindFirstValue(legacySessionIdClaim);
        return Guid.TryParse(sessionId, out Guid parsedSessionId)
            && await this.credentialSessionValidator.IsValidAsync(userId, new SessionId(parsedSessionId), this.HttpContext.RequestAborted);
    }

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
        IReadOnlyList<string>? originalPermissions = null, IReadOnlyList<string>? originalAudiences = null, IReadOnlyList<string>? userPermissions = null) =>
        this.resourcePermissionService is null
            ? Task.FromResult<Result<ResourceTokenProjection>>(Domain.Resources.ResourceAccessErrors.NotGranted)
            : this.resourcePermissionService.ProjectAsync(new ResourceTokenRequest(request.ClientId ?? string.Empty, scopes,
                request.GetResources(), userId, originalPermissions, originalAudiences, userPermissions), this.HttpContext.RequestAborted);

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
