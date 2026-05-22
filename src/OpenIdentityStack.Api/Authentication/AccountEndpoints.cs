using System.Globalization;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;

using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Infrastructure.Identity;

using AppPermissions = OpenIdentityStack.Application.Authorization.Permissions;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdentityStack.Api.Authentication;

/// <summary>
/// Minimal-API endpoints for the account flow (/Account/Login, /Account/Logout,
/// /Account/ExternalLogin, /Account/ExternalLoginCallback, /Account/AccessDenied,
/// /Account/CanAccessLocalLogin).
/// HTML pages are rendered as inline strings – no Razor / MVC dependency.
/// </summary>
internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Account/Login", GetLogin)
            .AllowAnonymous();

        app.MapPost("/Account/Login", PostLogin)
            .AllowAnonymous()
            .EnableRateLimiting("InteractiveLogin")
            .DisableAntiforgery();   // we validate the token manually inside the handler

        app.MapGet("/Account/ExternalLogin", ExternalLogin)
            .AllowAnonymous()
            .EnableRateLimiting("InteractiveLogin");

        app.MapGet("/Account/ExternalLoginCallback", ExternalLoginCallback)
            .AllowAnonymous();

        app.MapPost("/Account/Logout", Logout)
            .AllowAnonymous()
            .DisableAntiforgery();

        app.MapGet("/Account/AccessDenied", AccessDenied)
            .AllowAnonymous();

        app.MapPost("/Account/CanAccessLocalLogin", CanAccessLocalLogin)
            .AllowAnonymous();

        return app;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /Account/Login
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetLogin(
        HttpContext context,
        IAntiforgery antiforgery,
        [Microsoft.AspNetCore.Mvc.FromServices] IAuthenticationSettingsRepository authSettingsRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] IUpstreamProviderRepository providerRepository,
        string? returnUrl = null,
        bool fresh = false)
    {
        if (fresh && context.User.Identity?.IsAuthenticated == true)
        {
            await context.SignOutAsync("Cookies");
            await context.SignOutAsync("ExternalCookie");
            context.Response.Cookies.Delete(
                SessionManagementDefaults.SessionCookieName,
                SessionManagementDefaults.CreateSessionCookieOptions());
        }

        AuthenticationSettings settings = await authSettingsRepository.GetOrCreateAsync();
        bool isLocalDefault = settings.IsLocalDefault;
        bool localFallbackEnabled = settings.LocalFallbackEnabled;
        string? defaultProviderName = null;
        string defaultProviderDisplayName = "External Provider";

        if (!settings.IsLocalDefault && Guid.TryParse(settings.DefaultProviderId, out Guid providerId))
        {
            UpstreamProvider? provider = await providerRepository.GetByIdAsync(
                UpstreamProviderId.From(providerId));

            if (provider is not null && provider.IsActive)
            {
                defaultProviderName = provider.Name;
                defaultProviderDisplayName = provider.DisplayName;
            }
            else
            {
                isLocalDefault = true;
            }
        }

        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
        string html = LoginPageHtml(
            returnUrl,
            fresh,
            isLocalDefault,
            localFallbackEnabled,
            defaultProviderName,
            defaultProviderDisplayName,
            tokens.FormFieldName,
            tokens.RequestToken!,
            errorMessage: null,
            email: null);

        return Results.Content(html, "text/html");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /Account/Login
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> PostLogin(
        HttpContext context,
        IAntiforgery antiforgery,
        [Microsoft.AspNetCore.Mvc.FromServices] IAuthenticationSettingsRepository authSettingsRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] IUpstreamProviderRepository providerRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] IValidateUserCredentialsUseCase validateCredentialsUseCase,
        [Microsoft.AspNetCore.Mvc.FromServices] ICreateSessionUseCase createSessionUseCase)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest("Invalid request token.");
        }

        IFormCollection form = await context.Request.ReadFormAsync();
        string email = form["Email"].ToString();
        string password = form["Password"].ToString();
        bool rememberMe = string.Equals(form["RememberMe"].ToString(), "true", StringComparison.OrdinalIgnoreCase);
        string? returnUrl = form["returnUrl"].ToString() is { Length: > 0 } ru ? ru : null;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            AntiforgeryTokenSet retryTokens = antiforgery.GetAndStoreTokens(context);
            AuthenticationSettings settings = await authSettingsRepository.GetOrCreateAsync();
            string? defaultProviderName = null;
            string defaultProviderDisplayName = "External Provider";
            bool isLocalDefault = settings.IsLocalDefault;
            bool localFallbackEnabled = settings.LocalFallbackEnabled;

            if (!settings.IsLocalDefault && Guid.TryParse(settings.DefaultProviderId, out Guid providerId))
            {
                UpstreamProvider? provider = await providerRepository.GetByIdAsync(
                    UpstreamProviderId.From(providerId));
                if (provider is not null && provider.IsActive)
                {
                    defaultProviderName = provider.Name;
                    defaultProviderDisplayName = provider.DisplayName;
                }
                else
                {
                    isLocalDefault = true;
                }
            }

            string errorHtml = LoginPageHtml(
                returnUrl, false, isLocalDefault, localFallbackEnabled,
                defaultProviderName, defaultProviderDisplayName,
                retryTokens.FormFieldName, retryTokens.RequestToken!,
                "Email and password are required.", email);
            return Results.Content(errorHtml, "text/html");
        }

        var command = new ValidateUserCredentialsCommand(email, password);
        Result<ValidateUserCredentialsResult> result =
            await validateCredentialsUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            AntiforgeryTokenSet retryTokens = antiforgery.GetAndStoreTokens(context);
            AuthenticationSettings retrySettings = await authSettingsRepository.GetOrCreateAsync();
            string? defaultProviderName = null;
            string defaultProviderDisplayName = "External Provider";
            bool isLocalDefault = retrySettings.IsLocalDefault;
            bool localFallbackEnabled = retrySettings.LocalFallbackEnabled;

            if (!retrySettings.IsLocalDefault && Guid.TryParse(retrySettings.DefaultProviderId, out Guid providerId2))
            {
                UpstreamProvider? provider = await providerRepository.GetByIdAsync(
                    UpstreamProviderId.From(providerId2));
                if (provider is not null && provider.IsActive)
                {
                    defaultProviderName = provider.Name;
                    defaultProviderDisplayName = provider.DisplayName;
                }
                else
                {
                    isLocalDefault = true;
                }
            }

            string errorMessage = GetErrorMessage(result.Error.Code);
            string errorHtml = LoginPageHtml(
                returnUrl, false, isLocalDefault, localFallbackEnabled,
                defaultProviderName, defaultProviderDisplayName,
                retryTokens.FormFieldName, retryTokens.RequestToken!,
                errorMessage, email);
            return Results.Content(errorHtml, "text/html");
        }

        DateTimeOffset authenticationTime = DateTimeOffset.UtcNow;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Value.UserId.Value.ToString()),
            new(ClaimTypes.Email, result.Value.Email),
            new(ClaimTypes.Name, result.Value.DisplayName),
            new(Claims.AuthenticationTime,
                authenticationTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        string ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        string userAgent = context.Request.Headers.UserAgent.ToString();
        var sessionCommand = new CreateSessionCommand(result.Value.UserId, ipAddress, userAgent);
        Result<CreateSessionResult> sessionResult =
            await createSessionUseCase.ExecuteAsync(sessionCommand);

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
            IsPersistent = rememberMe,
            IssuedUtc = authenticationTime,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(rememberMe ? 24 : 1)
        };

        await context.SignInAsync("Cookies", claimsPrincipal, authProperties);
        string sessionCookieValue = SessionManagementDefaults.GenerateSessionCookieValue();
        context.Response.Cookies.Append(
            SessionManagementDefaults.SessionCookieName,
            sessionCookieValue,
            SessionManagementDefaults.CreateSessionCookieOptions(authProperties.ExpiresUtc));

        return RedirectToValidatedUrl(returnUrl);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /Account/ExternalLogin
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> ExternalLogin(
        HttpContext context,
        [Microsoft.AspNetCore.Mvc.FromServices] IUpstreamProviderRepository providerRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] IDynamicAuthenticationSchemeService schemeService,
        string? provider = null,
        string? returnUrl = null,
        bool fresh = false)
    {
        if (string.IsNullOrEmpty(provider))
        {
            return Results.BadRequest("Provider is required");
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            await context.SignOutAsync("Cookies");
            context.Response.Cookies.Delete(
                SessionManagementDefaults.SessionCookieName,
                SessionManagementDefaults.CreateSessionCookieOptions());
        }

        await context.SignOutAsync("ExternalCookie");

        UpstreamProvider? upstreamProvider = await providerRepository.GetByNameAsync(provider);
        if (upstreamProvider is null || !upstreamProvider.IsActive)
        {
            return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}&error=invalid_provider");
        }

        await schemeService.RegisterSchemeAsync(upstreamProvider);

        string callbackUrl = $"{context.Request.Scheme}://{context.Request.Host}/Account/ExternalLoginCallback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}&provider={Uri.EscapeDataString(provider)}";

        var properties = new AuthenticationProperties
        {
            RedirectUri = callbackUrl,
            Items =
            {
                { "returnUrl", returnUrl ?? "/" },
                { "provider", provider },
                { "providerId", upstreamProvider.Id.Value.ToString() }
            }
        };

        if (fresh)
        {
            properties.Items.Add("prompt", "login");
        }

        return Results.Challenge(properties, [provider]);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /Account/ExternalLoginCallback
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> ExternalLoginCallback(
        HttpContext context,
        [Microsoft.AspNetCore.Mvc.FromServices] IUpstreamProviderRepository providerRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] IUserRepository userRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] IJitProvisionUserUseCase jitProvisionUseCase,
        [Microsoft.AspNetCore.Mvc.FromServices] ICreateSessionUseCase createSessionUseCase,
        string? provider = null,
        string? returnUrl = null)
    {
        AuthenticateResult authenticateResult =
            await context.AuthenticateAsync("ExternalCookie");

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}&error=external_auth_failed");
        }

        ClaimsPrincipal externalUser = authenticateResult.Principal;
        string? subjectId = externalUser.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? externalUser.FindFirstValue("sub");
        string? email = externalUser.FindFirstValue(ClaimTypes.Email)
            ?? externalUser.FindFirstValue("email");
        string? name = externalUser.FindFirstValue(ClaimTypes.Name)
            ?? externalUser.FindFirstValue("name")
            ?? externalUser.FindFirstValue("preferred_username");

        if (string.IsNullOrEmpty(subjectId))
        {
            return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}&error=no_subject_claim");
        }

        string? providerIdString = authenticateResult.Properties?.Items["providerId"];
        Guid providerGuid;
        if (string.IsNullOrEmpty(providerIdString) || !Guid.TryParse(providerIdString, out providerGuid))
        {
            if (string.IsNullOrEmpty(provider))
            {
                return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}&error=provider_not_found");
            }

            UpstreamProvider? upstreamProvider = await providerRepository.GetByNameAsync(provider);
            if (upstreamProvider is null)
            {
                return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}&error=provider_not_found");
            }

            providerGuid = upstreamProvider.Id.Value;
        }

        var providerId = UpstreamProviderId.From(providerGuid);
        Domain.Users.User? user =
            await userRepository.FindByUpstreamIdentityAsync(providerId, subjectId);

        if (user is null)
        {
            var jitCommand = new JitProvisionUserCommand(providerId, subjectId, email, name);
            Result<JitProvisionUserResult> jitResult =
                await jitProvisionUseCase.ExecuteAsync(jitCommand);
            if (jitResult.IsFailure)
            {
                return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}&error=user_provisioning_failed");
            }

            user = await userRepository.GetByIdAsync(jitResult.Value.UserId);
            if (user is null)
            {
                return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}&error=user_not_found_after_provisioning");
            }
        }

        await context.SignOutAsync("ExternalCookie");

        DateTimeOffset authenticationTime = DateTimeOffset.UtcNow;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(Claims.AuthenticationTime,
                authenticationTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new("auth_method", "external"),
            new("provider", provider ?? string.Empty)
        };

        string ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        string userAgent = context.Request.Headers.UserAgent.ToString();
        var sessionCommand = new CreateSessionCommand(user.Id, ipAddress, userAgent);
        Result<CreateSessionResult> sessionResult =
            await createSessionUseCase.ExecuteAsync(sessionCommand);
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
            IssuedUtc = authenticationTime,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await context.SignInAsync("Cookies", claimsPrincipal, authProperties);
        string sessionCookieValue = SessionManagementDefaults.GenerateSessionCookieValue();
        context.Response.Cookies.Append(
            SessionManagementDefaults.SessionCookieName,
            sessionCookieValue,
            SessionManagementDefaults.CreateSessionCookieOptions(authProperties.ExpiresUtc));

        return RedirectToValidatedUrl(returnUrl);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /Account/Logout
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> Logout(HttpContext context)
    {
        await context.SignOutAsync("Cookies");
        context.Response.Cookies.Delete(
            SessionManagementDefaults.SessionCookieName,
            SessionManagementDefaults.CreateSessionCookieOptions());
        return Results.Redirect("/");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /Account/AccessDenied
    // ──────────────────────────────────────────────────────────────────────────

    private static IResult AccessDenied()
    {
        return Results.Content(AccessDeniedHtml(), "text/html");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /Account/CanAccessLocalLogin
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> CanAccessLocalLogin(
        [Microsoft.AspNetCore.Mvc.FromBody] CanAccessLocalLoginRequest? request,
        [Microsoft.AspNetCore.Mvc.FromServices] IAuthenticationSettingsRepository authSettingsRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] IUserRepository userRepository,
        [Microsoft.AspNetCore.Mvc.FromServices] IPermissionChecker permissionChecker)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.Ok(new CanAccessLocalLoginResponse(false));
        }

        AuthenticationSettings settings = await authSettingsRepository.GetOrCreateAsync();

        if (settings.IsLocalDefault)
        {
            return Results.Ok(new CanAccessLocalLoginResponse(true));
        }

        if (!settings.LocalFallbackEnabled)
        {
            return Results.Ok(new CanAccessLocalLoginResponse(false));
        }

        Domain.Users.User? user = await userRepository.GetByEmailAsync(request.Email);
        if (user is null)
        {
            return Results.Ok(new CanAccessLocalLoginResponse(false));
        }

        bool isAdmin = await permissionChecker.HasAnyPermissionAsync(
            user.Id,
            [AppPermissions.All, AppPermissions.System.All, AppPermissions.System.ManageSettings]);

        return Results.Ok(new CanAccessLocalLoginResponse(isAdmin));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static IResult RedirectToValidatedUrl(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl)
            && Uri.TryCreate(returnUrl, UriKind.Relative, out _)
            && returnUrl.StartsWith('/'))
        {
            return Results.Redirect(returnUrl);
        }

        return Results.Redirect("/");
    }

    private static string GetErrorMessage(string errorCode) =>
        errorCode switch
        {
            "Unauthorized.User.InvalidCredentials" => "Invalid email or password.",
            "Forbidden.User.AccountDisabled" => "Your account has been disabled. Please contact support.",
            "Forbidden.User.AccountNotVerified" => "Please verify your email address before logging in.",
            "Forbidden.AuthenticationSettings.LocalAuthNotPermitted" => "Authentication method not permitted.",
            _ => "An error occurred during login. Please try again."
        };

    // ──────────────────────────────────────────────────────────────────────────
    // Inline HTML builders (replaces Razor views, AOT-safe)
    // ──────────────────────────────────────────────────────────────────────────

    private static string LoginPageHtml(
        string? returnUrl,
        bool fresh,
        bool isLocalDefault,
        bool localFallbackEnabled,
        string? defaultProviderName,
        string defaultProviderDisplayName,
        string csrfFieldName,
        string csrfToken,
        string? errorMessage,
        string? email)
    {
        bool showExternalProvider = !isLocalDefault
            && !string.IsNullOrEmpty(defaultProviderName);
        bool showLocalForm = isLocalDefault || localFallbackEnabled;

        var sb = new StringBuilder();
        sb.Append($$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>Login – OpenIdentityStack</title>
              <style>{{SharedCss()}}</style>
            </head>
            <body>
              <div class="container">
                <div class="logo">
                  <h1>OpenIdentityStack</h1>
                  <p>Sign in to your account</p>
                </div>
            """);

        if (!string.IsNullOrEmpty(errorMessage))
        {
            sb.Append($"""

                <div class="validation-summary"><ul><li>{HtmlEncode(errorMessage)}</li></ul></div>
            """);
        }

        if (showExternalProvider)
        {
            string externalHref = $"/Account/ExternalLogin?provider={Uri.EscapeDataString(defaultProviderName!)}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}&fresh={fresh}";
            sb.Append($"""

                <div class="external-provider-section">
                  <a href="{HtmlEncode(externalHref)}" class="btn-primary btn-external">
                    Login with {HtmlEncode(defaultProviderDisplayName)}
                  </a>
                </div>
            """);

            if (localFallbackEnabled)
            {
                sb.Append("""

                    <div class="divider"><span>or</span></div>
                """);
            }
        }

        if (showLocalForm)
        {
            string submitLabel = showExternalProvider ? "Sign in with local account" : "Sign In";
            string submitClass = showExternalProvider ? "btn-primary btn-secondary-style" : "btn-primary";
            string emailValue = HtmlEncode(email ?? string.Empty);
            string returnUrlValue = HtmlEncode(returnUrl ?? string.Empty);

            sb.Append($$"""

                <form method="post" action="/Account/Login">
                  <input type="hidden" name="returnUrl" value="{{returnUrlValue}}" />
                  <input type="hidden" name="{{HtmlEncode(csrfFieldName)}}" value="{{HtmlEncode(csrfToken)}}" />

                  <div class="form-group">
                    <label for="Email">Email</label>
                    <input type="email" id="Email" name="Email" value="{{emailValue}}"
                           placeholder="Enter your email" autocomplete="email" required />
                  </div>

                  <div class="form-group">
                    <label for="Password">Password</label>
                    <input type="password" id="Password" name="Password"
                           placeholder="Enter your password" autocomplete="current-password" required />
                  </div>

                  <div class="form-check">
                    <input type="checkbox" id="RememberMe" name="RememberMe" value="true" />
                    <label for="RememberMe">Remember me</label>
                  </div>

                  <button type="submit" class="{{submitClass}}">{{HtmlEncode(submitLabel)}}</button>
                </form>
            """);
        }

        sb.Append("""

              </div>
            </body>
            </html>
            """);

        return sb.ToString();
    }

    private static string AccessDeniedHtml() => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>Access Denied – OpenIdentityStack</title>
          <style>{{SharedCss()}}</style>
        </head>
        <body>
          <div class="container">
            <div class="logo"><h1>Access Denied</h1></div>
            <p style="text-align:center;color:#666">
              You do not have permission to access this resource.
            </p>
            <div style="text-align:center;margin-top:20px">
              <a href="/" style="color:#0066cc;text-decoration:none">Return to Home</a>
            </div>
          </div>
        </body>
        </html>
        """;

    private static string SharedCss() => """
        *,*::before,*::after{box-sizing:border-box}
        body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;
             background-color:#f5f5f5;margin:0;padding:0;min-height:100vh;
             display:flex;align-items:center;justify-content:center}
        .container{background:white;border-radius:8px;box-shadow:0 2px 10px rgba(0,0,0,.1);
                   padding:40px;width:100%;max-width:400px;margin:20px}
        .logo{text-align:center;margin-bottom:30px}
        .logo h1{color:#333;font-size:24px;margin:0}
        .logo p{color:#666;margin:8px 0 0}
        .form-group{margin-bottom:20px}
        .form-group label{display:block;margin-bottom:8px;font-weight:500;color:#333}
        .form-group input[type=email],.form-group input[type=password]{
          width:100%;padding:12px;border:1px solid #ddd;border-radius:4px;
          font-size:16px;transition:border-color .2s}
        .form-group input:focus{outline:none;border-color:#0066cc;
          box-shadow:0 0 0 3px rgba(0,102,204,.1)}
        .form-check{display:flex;align-items:center;margin-bottom:20px}
        .form-check input{margin-right:8px}
        .btn-primary{width:100%;padding:14px;background-color:#0066cc;color:white;border:none;
                     border-radius:4px;font-size:16px;font-weight:500;cursor:pointer;
                     transition:background-color .2s;display:block;text-align:center;
                     text-decoration:none}
        .btn-primary:hover{background-color:#0052a3}
        .btn-secondary-style{background-color:#6c757d}
        .btn-secondary-style:hover{background-color:#5a6268}
        .btn-external{margin-bottom:15px}
        .external-provider-section{text-align:center;margin-bottom:20px}
        .divider{display:flex;align-items:center;text-align:center;margin:25px 0}
        .divider::before,.divider::after{content:'';flex:1;border-bottom:1px solid #ddd}
        .divider span{padding:0 15px;color:#999;font-size:14px}
        .validation-summary{background-color:#f8d7da;border:1px solid #f5c6cb;
          color:#721c24;padding:12px;border-radius:4px;margin-bottom:20px}
        .validation-summary ul{margin:0;padding-left:20px}
        """;

    private static string HtmlEncode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#x27;", StringComparison.Ordinal);
    }
}

/// <summary>Response for the CanAccessLocalLogin endpoint.</summary>
/// <param name="CanAccess">Whether the user can access local login.</param>
public sealed record CanAccessLocalLoginResponse(bool CanAccess);
