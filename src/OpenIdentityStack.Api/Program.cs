using OpenIdentityStack.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using OpenIdentityStack.Infrastructure;
using OpenIdentityStack.Application;
using OpenIdentityStack.Api.Configuration;
using OpenIdentityStack.Api.Authentication;
using Scalar.AspNetCore;
using Microsoft.Extensions.Primitives;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Api.Admin;
using OpenIdentityStack.Api.Applications;
using OpenIdentityStack.Api.Audit;
using OpenIdentityStack.Api.CurrentUser;
using OpenIdentityStack.Api.Users;
using OpenIdentityStack.Api.Groups;
using OpenIdentityStack.Api.Sessions;
using OpenIdentityStack.Api.Federation;
using OpenIdentityStack.Api.Settings;
using OpenIdentityStack.Infrastructure.Identity;
using Microsoft.Extensions.Hosting.WindowsServices;
using OpenIdentityStack.Infrastructure.Persistence;
 
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (WindowsServiceHelpers.IsWindowsService())
{
    builder.Host.UseWindowsService(options => options.ServiceName = "OpenIdentityStack");
    builder.Host.UseContentRoot(AppContext.BaseDirectory);
}

// Add Aspire service defaults (health checks, OpenTelemetry, resilience)
builder.AddServiceDefaults();

// Add PostgreSQL health check for readiness
builder.AddPostgreSqlHealthCheck("openidentitystack");

// Add Application services
builder.Services.AddApplication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<OpenIdentityStack.Application.Abstractions.IAdministrativeActorContext, AdministrativeActorContext>();

// Add Infrastructure services (DbContext, OpenIddict, etc.)
builder.Services.AddInfrastructureWithAspire(builder.Configuration, builder.Environment);

if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    builder.Services.AddConfiguredForwardedHeaders(builder.Configuration);
}

// Add API services with camelCase JSON serialization for frontend compatibility
builder.Services.AddControllersWithViews()
    .AddDefaultJsonOptions();
builder.Services.AddDefaultHttpJsonOptions(); // Also configure Minimal API JSON serialization
builder.Services.AddRazorPages();
builder.Services.AddOpenApi();
builder.Services.AddConfiguredRateLimiting(builder.Environment);
builder.Services.AddConfiguredProblemDetails();

builder.Services.AddDataProtection()
    .SetApplicationName("OpenIdentityStack")
    .PersistKeysToDbContext<OpenIdentityStackDbContext>();

builder.Services.AddScoped<IOpenIddictRequestService, OpenIddictRequestService>();
builder.Services.AddScoped<ITokenClaimProjectionService, TokenClaimProjectionService>();

// Add authentication and authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    options.DefaultChallengeScheme = "Cookies";
})
.AddCookie("Cookies", options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
})
.AddExternalCookie(builder.Environment); // Add external cookie for OAuth callback flow

// NOTE: Rate limiting for authentication endpoints
// Implemented via EnableRateLimiting attribute on AccountController methods
// Configuration:
// - Login endpoint: 5 concurrent requests
// - External login: 10 concurrent requests
// Additional protection should be implemented at infrastructure layer (reverse proxy/WAF)

// Add dynamic external authentication (loads providers from database at startup)
builder.Services.AddDynamicExternalAuthentication();

builder.Services.AddAuthorization(options =>
{
    options.AddPermissionPolicies();
});
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

// T043: Configure CORS for Management Web
// In development/testing, allow dynamic origins (Aspire assigns random ports)
// In production, configure specific allowed origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("ManagementWeb", policy =>
    {
        if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        {
            // Allow any origin in development for flexibility with dynamic ports
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // In production, configure specific origins from environment variable
            string? allowedOrigins = builder.Configuration["AllowedCorsOrigins"];
            if (!string.IsNullOrEmpty(allowedOrigins))
            {
                policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries))
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
        }
    });
});

WebApplication app = builder.Build();

// Database migrations and seeding are performed by external tools.

if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders();
}

// ProblemDetails middleware for consistent error responses
app.UseExceptionHandler();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Serve static files
app.UseStaticFiles();

// Routing
app.UseRouting();

// Enable CORS for Management Web
app.UseCors("ManagementWeb");

// Add security headers
// Keep the default CSP until Razor/OpenIddict pages that require inline scripts
// are refactored to use CSP nonces or hashes.
app.UseSecurityHeaders();

app.UseRateLimiter();

app.UseAuthorizationErrorRedirects();

// Debug logging for authorization headers (development only)
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
#pragma warning disable CA1848, CA1873
        ILogger<Program> logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        foreach (KeyValuePair<string, StringValues> header in context.Request.Headers)
        {
            logger.LogInformation("Header: {Key}={Value}", header.Key, header.Value);
        }
        string authHeader = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader))
        {
            logger.LogInformation("Authorization Header found: {HeaderPrefix}...", authHeader.Substring(0, Math.Min(20, authHeader.Length)));
        }
        else
        {
            logger.LogWarning("Authorization Header MISSING on request to {Path}", context.Request.Path);
        }
#pragma warning restore CA1848, CA1873
        await next();
    });
}

// Authentication and Authorization middleware
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    IAdministrativeApproval approval = context.RequestServices.GetRequiredService<IAdministrativeApproval>();
    try
    {
        await next(context);
        await approval.RecordOutcomeAsync(context.Response.StatusCode < 400, CancellationToken.None);
    }
    catch
    {
        await approval.RecordOutcomeAsync(false, CancellationToken.None);
        throw;
    }
});

// Map MVC Controllers for authentication endpoints (/connect/*, /Account/*)
// These handle OpenIddict OAuth2/OIDC flows and login UI
app.MapControllers();

// Map Minimal API endpoints
app.MapCurrentUserApi();
app.MapApplicationsApi();
app.MapUsersApi();
app.MapPublicProfilesApi();
app.MapRolesApi();
app.MapGroupsApi();
app.MapSessionsApi();
app.MapPermissionsApi();
app.MapApplicationPermissionsApi();
app.MapProvidersApi();
app.MapAuthenticationSettingsApi();
app.MapAuditEntriesApi();

// Map Razor Pages for login UI
app.MapRazorPages();

// Map Aspire default endpoints (health, alive)
app.MapDefaultEndpoints();

app.MapGet("/", () => "OpenIdentityStack API is running")
    .WithName("Root");

await app.RunAsync();

public partial class Program;
