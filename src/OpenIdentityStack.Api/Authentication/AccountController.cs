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

    public AccountController(
        IValidateUserCredentialsUseCase validateCredentialsUseCase,
        ICreateSessionUseCase createSessionUseCase,
        IAuthenticationSettingsRepository authSettingsRepository,
        IUpstreamProviderRepository providerRepository,
        IPermissionChecker permissionChecker,
        IUserRepository userRepository,
        IDynamicAuthenticationSchemeService schemeService,
        IJitProvisionUserUseCase jitProvisionUseCase)
    {
        this.validateCredentialsUseCase = validateCredentialsUseCase;
        this.createSessionUseCase = createSessionUseCase;
        this.authSettingsRepository = authSettingsRepository;
        this.providerRepository = providerRepository;
        this.permissionChecker = permissionChecker;
        this.userRepository = userRepository;
        this.schemeService = schemeService;
        this.jitProvisionUseCase = jitProvisionUseCase;
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

        ClaimsPrincipal externalUser = authenticateResult.Principal;

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

        // Get the provider ID from the authentication properties
        string? providerIdString = authenticateResult.Properties?.Items["providerId"];
        if (string.IsNullOrEmpty(providerIdString) || !Guid.TryParse(providerIdString, out Guid providerGuid))
        {
            // Try to look up provider by name
            UpstreamProvider? upstreamProvider = await this.providerRepository.GetByNameAsync(provider);
            if (upstreamProvider is null)
            {
                return this.RedirectToAction(nameof(Login), new { returnUrl, error = "provider_not_found" });
            }
            providerGuid = upstreamProvider.Id.Value;
        }

        var providerId = UpstreamProviderId.From(providerGuid);

        // Find or create user via upstream identity
        Domain.Users.User? user = await this.userRepository.FindByUpstreamIdentityAsync(providerId, subjectId);

        if (user is null)
        {
            // JIT provision a new user
            var jitCommand = new JitProvisionUserCommand(
                providerId,
                subjectId,
                email,
                name);

            Result<JitProvisionUserResult> jitResult = await this.jitProvisionUseCase.ExecuteAsync(jitCommand);
            if (jitResult.IsFailure)
            {
                return this.RedirectToAction(nameof(Login), new { returnUrl, error = "user_provisioning_failed" });
            }

            // Fetch the newly created user
            user = await this.userRepository.GetByIdAsync(jitResult.Value.UserId);
            if (user is null)
            {
                return this.RedirectToAction(nameof(Login), new { returnUrl, error = "user_not_found_after_provisioning" });
            }
        }

        // Sign out of the external cookie
        await this.HttpContext.SignOutAsync("ExternalCookie");

        // Create claims for the authenticated user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new("auth_method", "external"),
            new("provider", provider)
        };

        // Create user session
        string ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        string userAgent = this.Request.Headers.UserAgent.ToString();
        var createSessionCommand = new CreateSessionCommand(
            user.Id,
            ipAddress,
            userAgent);

        Result<CreateSessionResult> sessionResult = await this.createSessionUseCase.ExecuteAsync(createSessionCommand);
        if (sessionResult.IsSuccess)
        {
            string sessionId = sessionResult.Value.SessionId.Value.ToString();
            claims.Add(new Claim("sid", sessionId));
            claims.Add(new Claim("session_id", sessionId));
        }

        var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await this.HttpContext.SignInAsync("Cookies", claimsPrincipal, authProperties);
        string sessionCookieValue = SessionManagementDefaults.GenerateSessionCookieValue();
        this.HttpContext.Response.Cookies.Append(
            SessionManagementDefaults.SessionCookieName,
            sessionCookieValue,
            SessionManagementDefaults.CreateSessionCookieOptions(authProperties.ExpiresUtc));

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

        var command = new ValidateUserCredentialsCommand(model.Email, model.Password);
        Result<ValidateUserCredentialsResult> result = await this.validateCredentialsUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            this.ModelState.AddModelError(string.Empty, GetErrorMessage(result.Error.Code));
            return this.View(model);
        }

        // Create claims for the authenticated user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Value.UserId.Value.ToString()),
            new(ClaimTypes.Email, result.Value.Email),
            new(ClaimTypes.Name, result.Value.DisplayName)
        };

        // Create user session
        string ipAddress = this.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        string userAgent = this.Request.Headers.UserAgent.ToString();
        var createSessionCommand = new CreateSessionCommand(
            result.Value.UserId,
            ipAddress,
            userAgent);

        Result<CreateSessionResult> sessionResult = await this.createSessionUseCase.ExecuteAsync(createSessionCommand);
        if (sessionResult.IsSuccess)
        {
            string sessionId = sessionResult.Value.SessionId.Value.ToString();
            claims.Add(new Claim("sid", sessionId));
            claims.Add(new Claim("session_id", sessionId));
        }

        var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe == true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe == true ? 24 : 1)
        };

        await this.HttpContext.SignInAsync("Cookies", claimsPrincipal, authProperties);
        string sessionCookieValue = SessionManagementDefaults.GenerateSessionCookieValue();
        this.HttpContext.Response.Cookies.Append(
            SessionManagementDefaults.SessionCookieName,
            sessionCookieValue,
            SessionManagementDefaults.CreateSessionCookieOptions(authProperties.ExpiresUtc));

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

    /// <summary>
    /// Checks if the current user can access local login fallback.
    /// This is called via AJAX to determine if the local login link should be shown.
    /// Uses POST to avoid exposing email in URL/logs.
    /// </summary>
    [HttpPost("~/Account/CanAccessLocalLogin")]
    public async Task<IActionResult> CanAccessLocalLogin([FromBody] CanAccessLocalLoginRequest? request)
    {
        // If no email provided, we can't determine admin status
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
        {
            return this.Json(new { canAccess = false });
        }

        string email = request.Email;

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
            "Forbidden.User.AccountDisabled" => "Your account has been disabled. Please contact support.",
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
