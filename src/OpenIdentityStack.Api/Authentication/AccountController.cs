using System.Globalization;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Settings;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OpenIdentityStack.Infrastructure.Identity;
using AppPermissions = OpenIdentityStack.Application.Authorization.Permissions;

using SharedKernel;
namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Controller for user account operations (login, logout).
/// </summary>
public class AccountController : Controller
{
    private readonly IValidateUserCredentialsUseCase validateCredentialsUseCase;
    private readonly ICreateSessionUseCase createSessionUseCase;
    private readonly IAuthenticationSettingsRepository authSettingsRepository;
    private readonly IUpstreamProviderRepository providerRepository;
    private readonly IPermissionChecker permissionChecker;
    private readonly IUserRepository userRepository;
    private readonly IDynamicAuthenticationSchemeService schemeService;
    private readonly IJitProvisionUserUseCase jitProvisionUseCase;
    private readonly IAuditLog audit;
    private readonly ICredentialBoundaryStore credentialBoundary;
    private readonly ISessionMonitoringCookieService sessionMonitoringCookies;

    public AccountController(
        IValidateUserCredentialsUseCase validateCredentialsUseCase,
        ICreateSessionUseCase createSessionUseCase,
        IAuthenticationSettingsRepository authSettingsRepository,
        IUpstreamProviderRepository providerRepository,
        IPermissionChecker permissionChecker,
        IUserRepository userRepository,
        IDynamicAuthenticationSchemeService schemeService,
        IJitProvisionUserUseCase jitProvisionUseCase,
        IAuditLog audit,
        ICredentialBoundaryStore credentialBoundary,
        ISessionMonitoringCookieService sessionMonitoringCookies)
    {
        this.validateCredentialsUseCase = validateCredentialsUseCase;
        this.createSessionUseCase = createSessionUseCase;
        this.authSettingsRepository = authSettingsRepository;
        this.providerRepository = providerRepository;
        this.permissionChecker = permissionChecker;
        this.userRepository = userRepository;
        this.schemeService = schemeService;
        this.jitProvisionUseCase = jitProvisionUseCase;
        this.audit = audit;
        this.credentialBoundary = credentialBoundary;
        this.sessionMonitoringCookies = sessionMonitoringCookies;
    }

    /// <summary>
    /// Displays the login page.
    /// </summary>
    /// <param name="returnUrl">The URL to return to after login.</param>
    /// <param name="fresh">If true, clears any existing session to ensure a fresh login.</param>
    [HttpGet("~/Account/Login")]
    public async Task<IActionResult> Login(string? returnUrl = null, bool fresh = false)
    {
        // If fresh login is requested, sign out any existing session first
        // This ensures switching from local to external provider works correctly
        if (fresh && this.User.Identity?.IsAuthenticated == true)
        {
            await this.HttpContext.SignOutAsync("Cookies");
            await this.HttpContext.SignOutAsync("ExternalCookie");
            this.HttpContext.Response.Cookies.Delete(
                SessionManagementDefaults.SessionCookieName,
                SessionManagementDefaults.CreateSessionCookieOptions());
        }

        this.ViewData["ReturnUrl"] = returnUrl;
        // Pass fresh parameter to the view so it can forward it to ExternalLogin
        this.ViewData["Fresh"] = fresh;

        // Get authentication settings
        AuthenticationSettings settings = await this.authSettingsRepository.GetOrCreateAsync();
        
        this.ViewData["IsLocalDefault"] = settings.IsLocalDefault;
        this.ViewData["LocalFallbackEnabled"] = settings.LocalFallbackEnabled;

        if (!settings.IsLocalDefault)
        {
            // Get the default provider information
            if (Guid.TryParse(settings.DefaultProviderId, out Guid providerId))
            {
                UpstreamProvider? provider = await this.providerRepository.GetByIdAsync(
                    UpstreamProviderId.From(providerId));
                
                if (provider is not null && provider.IsActive)
                {
                    this.ViewData["DefaultProviderName"] = provider.Name;
                    this.ViewData["DefaultProviderDisplayName"] = provider.DisplayName;
                }
                else
                {
                    // Provider not found or disabled, fall back to local
                    this.ViewData["IsLocalDefault"] = true;
                }
            }
        }

        return this.View();
    }

    /// <summary>
    /// Initiates external authentication with the specified provider.
    /// </summary>
    [HttpGet("~/Account/ExternalLogin")]
    [EnableRateLimiting("InteractiveLogin")]
    
    public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl = null, bool fresh = false)
    {
        if (string.IsNullOrEmpty(provider))
        {
            return this.BadRequest("Provider is required");
        }

        // Clear any existing session to ensure fresh external authentication
        // This is critical when switching from local to external provider
        if (this.User.Identity?.IsAuthenticated == true)
        {
            await this.HttpContext.SignOutAsync("Cookies");
            this.HttpContext.Response.Cookies.Delete(
                SessionManagementDefaults.SessionCookieName,
                SessionManagementDefaults.CreateSessionCookieOptions());
        }

        // Also clear any stale external cookie from previous attempts
        await this.HttpContext.SignOutAsync("ExternalCookie");

        // Validate the provider exists and is active
        UpstreamProvider? upstreamProvider = await this.providerRepository.GetByNameAsync(provider);
        if (upstreamProvider is null || !upstreamProvider.IsActive)
        {
            // Provider not found or disabled, redirect to login with error
            return this.RedirectToAction(nameof(Login), new { returnUrl, error = "invalid_provider" });
        }

        // Ensure the authentication scheme is registered (handles newly added providers)
        await this.schemeService.RegisterSchemeAsync(upstreamProvider);

        // Build the callback URL
        string callbackUrl = this.Url.Action(
            nameof(ExternalLoginCallback), 
            "Account", 
            new { returnUrl, provider },
            this.Request.Scheme) ?? "/";

        // Challenge the external authentication scheme
        AuthenticationProperties properties = new()
        {
            RedirectUri = callbackUrl,
            Items =
            {
                { "returnUrl", returnUrl ?? "/" },
                { "provider", provider },
                { "providerId", upstreamProvider.Id.Value.ToString() }
            }
        };

        properties.SetString(CredentialBoundaryClaims.Epoch, (await this.credentialBoundary.GetEpochAsync(this.HttpContext.RequestAborted)).ToString());

        // If fresh login is required, pass prompt=login to the upstream IdP
        // This forces the external IdP (like Authentik) to show its login page
        if (fresh)
        {
            properties.Items.Add("prompt", "login");
        }

        return this.Challenge(properties, provider);
    }

    /// <summary>
    /// Handles the callback from external authentication providers.
    /// </summary>
    [HttpGet("~/Account/ExternalLoginCallback")]
    public async Task<IActionResult> ExternalLoginCallback(string provider, string? returnUrl = null)
    {
        // Authenticate using the external cookie scheme
        AuthenticateResult authenticateResult = await this.HttpContext.AuthenticateAsync("ExternalCookie");

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            return this.RedirectToAction(nameof(Login), new { returnUrl, error = "external_auth_failed" });
        }

        // Consume the authenticated external ticket before any validation or JIT denial can return.
        await this.HttpContext.SignOutAsync("ExternalCookie");
        ClaimsPrincipal externalUser = authenticateResult.Principal;
        string? credentialEpoch = authenticateResult.Properties?.GetString(CredentialBoundaryClaims.Epoch);
        if (!await this.credentialBoundary.IsCurrentAsync(credentialEpoch, this.HttpContext.RequestAborted))
        {
            return this.RedirectToAction(nameof(Login), new { returnUrl, error = "external_auth_failed" });
        }
        string? verifiedEvidenceEmail = null;
        authenticateResult.Properties?.Items.TryGetValue(ExternalIdentityProperties.VerifiedEmail, out verifiedEvidenceEmail);

        // Extract claims from the external identity
        string? subjectId = externalUser.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? externalUser.FindFirstValue("sub");
        string? email = externalUser.FindFirstValue(ClaimTypes.Email)
            ?? externalUser.FindFirstValue("email");
        string? name = externalUser.FindFirstValue(ClaimTypes.Name)
            ?? externalUser.FindFirstValue("name")
            ?? externalUser.FindFirstValue("preferred_username");

        if (string.IsNullOrEmpty(subjectId))
        {
            return this.RedirectToAction(nameof(Login), new { returnUrl, error = "no_subject_claim" });
        }

        string? providerIdString = authenticateResult.Properties?.GetString(ExternalIdentityProperties.ProviderId);
        string? authenticatedProvider = authenticateResult.Properties?.GetString(ExternalIdentityProperties.ProviderName);
        string? issuer = authenticateResult.Properties?.GetString(ExternalIdentityProperties.ValidatedIssuer);
        string? authority = authenticateResult.Properties?.GetString(ExternalIdentityProperties.Authority);
        bool providerIdIsValid = Guid.TryParse(providerIdString, out Guid providerGuid);
        if (!providerIdIsValid || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(authenticatedProvider))
        {
            await this.audit.LogAsync(
                "federation",
                "Federation.IssuerBindingRejected",
                "UpstreamProvider",
                providerIdIsValid ? providerGuid.ToString() : "unknown",
                "Protected external identity binding metadata is missing or invalid.",
                this.HttpContext.RequestAborted);
            return this.RedirectToAction(nameof(Login), new { returnUrl, error = "external_auth_failed" });
        }

        // Existing and new identities pass the same issuer-bound authentication checks.
        var jitCommand = new JitProvisionUserCommand(UpstreamProviderId.From(providerGuid), subjectId, email, name, issuer, authority,
            !string.IsNullOrWhiteSpace(email) && string.Equals(verifiedEvidenceEmail?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase));
        Result<JitProvisionUserResult> jitResult = await this.jitProvisionUseCase.ExecuteAsync(jitCommand);
        if (jitResult.IsFailure)
        {
            return this.RedirectToAction(nameof(Login), new { returnUrl, error = "external_auth_failed" });
        }

        Domain.Users.User? user = await this.userRepository.GetByIdAsync(jitResult.Value.UserId);
        if (user is null)
        {
            return this.RedirectToAction(nameof(Login), new { returnUrl, error = "external_auth_failed" });
        }

        if (user.Status != Domain.Users.UserStatus.Active)
        {
            await this.audit.LogAsync("federation", "Federation.AccountAssociationDenied", "User", user.Id.Value.ToString(),
                $"Local account is not active. Provider: {providerGuid}.");
            return this.RedirectToAction(nameof(Login), new { returnUrl, error = "external_auth_failed" });
        }

        DateTimeOffset authenticationTime = DateTimeOffset.UtcNow;

        // Create claims for the authenticated user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(Claims.AuthenticationTime, authenticationTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("auth_method", "external"),
            new(CredentialBoundaryClaims.Epoch, credentialEpoch ?? Guid.Empty.ToString()),
            new(OpenIdentityStack.Api.Authorization.AdministrativeActorContext.HumanSubjectClaim, user.Id.Value.ToString()),
            new("provider", authenticatedProvider)
        };

        // Only the upstream's validated authentication event can establish freshness; receiving an SSO callback cannot.
        string? upstreamAuthenticationTime = authenticateResult.Properties?.GetString(ExternalIdentityProperties.AuthenticationTime);
        if (upstreamAuthenticationTime is not null)
        {
            claims.Add(new Claim(OpenIdentityStack.Api.Authorization.AdministrativeActorContext.HumanAuthenticationClaim, upstreamAuthenticationTime));
        }

        // Create user session
        string ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        string userAgent = this.Request.Headers.UserAgent.ToString();
        var createSessionCommand = new CreateSessionCommand(
            user.Id,
            ipAddress,
            userAgent);

        Result<CreateSessionResult> sessionResult = await this.createSessionUseCase.ExecuteAsync(createSessionCommand);
        if (sessionResult.IsFailure)
        {
            return this.RedirectToAction(nameof(Login), new { returnUrl, error = "external_auth_failed" });
        }
        string sessionId = sessionResult.Value.SessionId.Value.ToString();
        claims.Add(new Claim("sid", sessionId));
        claims.Add(new Claim("session_id", sessionId));

        var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = authenticationTime,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await this.HttpContext.SignInAsync("Cookies", claimsPrincipal, authProperties);
        Guid capturedEpoch = Guid.TryParse(credentialEpoch, out Guid parsedEpoch) ? parsedEpoch : Guid.Empty;
        this.AppendSessionMonitoringCookie(
            user.Id, sessionResult.Value.SessionId, capturedEpoch, authProperties.ExpiresUtc!.Value);

        return this.RedirectToValidatedUrl(returnUrl);
    }

    /// <summary>
    /// Handles login form submission.
    /// </summary>
    [HttpPost("~/Account/Login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("InteractiveLogin")]
    
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        this.ViewData["ReturnUrl"] = returnUrl;

        if (!this.ModelState.IsValid)
        {
            return this.View(model);
        }

        Guid credentialEpoch = await this.credentialBoundary.GetEpochAsync(this.HttpContext.RequestAborted);
        var command = new ValidateUserCredentialsCommand(model.Email, model.Password);
        Result<ValidateUserCredentialsResult> result = await this.validateCredentialsUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            this.ModelState.AddModelError(string.Empty, GetErrorMessage(result.Error.Code));
            return this.View(model);
        }

        DateTimeOffset authenticationTime = DateTimeOffset.UtcNow;

        // Create claims for the authenticated user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Value.UserId.Value.ToString()),
            new(CredentialBoundaryClaims.Epoch, credentialEpoch.ToString()),
            new(ClaimTypes.Email, result.Value.Email),
            new(ClaimTypes.Name, result.Value.DisplayName),
            new(Claims.AuthenticationTime, authenticationTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(OpenIdentityStack.Api.Authorization.AdministrativeActorContext.HumanSubjectClaim, result.Value.UserId.Value.ToString()),
            new(OpenIdentityStack.Api.Authorization.AdministrativeActorContext.HumanAuthenticationClaim, authenticationTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };

        // Create user session
        string ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        string userAgent = this.Request.Headers.UserAgent.ToString();
        var createSessionCommand = new CreateSessionCommand(
            result.Value.UserId,
            ipAddress,
            userAgent);

        Result<CreateSessionResult> sessionResult = await this.createSessionUseCase.ExecuteAsync(createSessionCommand);
        if (sessionResult.IsFailure)
        {
            this.ModelState.AddModelError(string.Empty, "Unable to complete sign-in. Please try again.");
            return this.View(model);
        }
        string sessionId = sessionResult.Value.SessionId.Value.ToString();
        claims.Add(new Claim("sid", sessionId));
        claims.Add(new Claim("session_id", sessionId));

        var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe == true,
            IssuedUtc = authenticationTime,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe == true ? 24 : 1)
        };

        await this.HttpContext.SignInAsync("Cookies", claimsPrincipal, authProperties);
        this.AppendSessionMonitoringCookie(
            result.Value.UserId, sessionResult.Value.SessionId, credentialEpoch, authProperties.ExpiresUtc!.Value);

        return this.RedirectToValidatedUrl(returnUrl);
    }

    /// <summary>
    /// Handles logout.
    /// </summary>
    [HttpPost("~/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await this.HttpContext.SignOutAsync("Cookies");
        this.HttpContext.Response.Cookies.Delete(
            SessionManagementDefaults.SessionCookieName,
            SessionManagementDefaults.CreateSessionCookieOptions());
        return this.Redirect("/");
    }

    /// <summary>
    /// Displays access denied page.
    /// </summary>
    [HttpGet("~/Account/AccessDenied")]
    public IActionResult AccessDenied()
    {
        return this.View();
    }

    private void AppendSessionMonitoringCookie(
        UserId userId,
        SessionId sessionId,
        Guid credentialEpoch,
        DateTimeOffset expiresUtc)
    {
        string value = this.sessionMonitoringCookies.Create(userId, sessionId, credentialEpoch, expiresUtc);
        this.HttpContext.Response.Cookies.Append(
            SessionManagementDefaults.SessionCookieName,
            value,
            SessionManagementDefaults.CreateSessionCookieOptions(expiresUtc));
    }

    /// <summary>
    /// Checks if the current user can access local login fallback.
    /// This is called via AJAX to determine if the local login link should be shown.
    /// Uses POST to avoid exposing email in URL/logs.
    /// </summary>
    [HttpPost("~/Account/CanAccessLocalLogin")]
    public async Task<IActionResult> CanAccessLocalLogin([FromBody] CanAccessLocalLoginRequest? request)
    {
        if (!this.ModelState.IsValid)
        {
            return this.Json(new { canAccess = false });
        }

        // If no email provided, we can't determine admin status
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
        {
            return this.Json(new { canAccess = false });
        }

        string email = request.Email.Trim();

        // Get authentication settings
        AuthenticationSettings settings = await this.authSettingsRepository.GetOrCreateAsync();

        // If local is default, anyone can access local login
        if (settings.IsLocalDefault)
        {
            return this.Json(new { canAccess = true });
        }

        // If local fallback is disabled, no one can access
        if (!settings.LocalFallbackEnabled)
        {
            return this.Json(new { canAccess = false });
        }

        // Check if user exists and is an IAM admin
        Domain.Users.User? user = await this.userRepository.GetByEmailAsync(email);
        if (user is null)
        {
            // Don't reveal user doesn't exist - just say no access
            return this.Json(new { canAccess = false });
        }

        bool isAdmin = await this.permissionChecker.HasAnyPermissionAsync(
            user.Id,
            [AppPermissions.All, AppPermissions.System.All, AppPermissions.System.ManageSettings]);

        return this.Json(new { canAccess = isAdmin });
    }

    /// <summary>
    /// Safely redirects to a validated URL, preventing open redirect attacks.
    /// Uses LocalRedirect which throws an exception if the URL is not local.
    /// </summary>
    private IActionResult RedirectToValidatedUrl(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && this.Url.IsLocalUrl(returnUrl))
        {
            try
            {
                // LocalRedirect throws InvalidOperationException if URL is not local
                return this.LocalRedirect(returnUrl);
            }
            catch (InvalidOperationException)
            {
                // If somehow a non-local URL passed IsLocalUrl, redirect to home
                return this.Redirect("/");
            }
        }

        return this.Redirect("/");
    }

    private static string GetErrorMessage(string errorCode)
    {
        return errorCode switch
        {
            "Unauthorized.User.InvalidCredentials" => "Invalid email or password.",
            "Forbidden.User.AccountDisabled" => "Invalid email or password.",
            "Forbidden.User.AccountNotVerified" => "Please verify your email address before logging in.",
            "Forbidden.AuthenticationSettings.LocalAuthNotPermitted" => "Authentication method not permitted.",
            _ => "An error occurred during login. Please try again."
        };
    }
}

/// <summary>
/// View model for the login form.
/// </summary>
public sealed class LoginViewModel
{
    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether to remember the login.
    /// </summary>
    public bool? RememberMe { get; set; }
}

/// <summary>
/// Request model for checking if local login is accessible.
/// </summary>
public sealed class CanAccessLocalLoginRequest
{
    /// <summary>
    /// Gets or sets the email address to check.
    /// </summary>
    public string? Email { get; set; }
}
