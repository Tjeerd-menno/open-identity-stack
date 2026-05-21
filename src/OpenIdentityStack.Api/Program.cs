using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using OpenIdentityStack.Infrastructure;
using OpenIdentityStack.Application;
using OpenIdentityStack.Api.Configuration;
using OpenIdentityStack.Api.Serialization;
using OpenIdentityStack.Api.Authentication;
using Scalar.AspNetCore;
using Microsoft.Extensions.Primitives;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Api.Admin;
using OpenIdentityStack.Api.Clients;
using OpenIdentityStack.Api.Users;
using OpenIdentityStack.Api.Groups;
using OpenIdentityStack.Api.Sessions;
using OpenIdentityStack.Api.ServiceAccounts;
using OpenIdentityStack.Api.Federation;
using OpenIdentityStack.Api.Settings;
using OpenIdentityStack.Infrastructure.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.ServiceAccounts;
using OpenIdentityStack.Domain.ServicePermissions;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
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

// Add Infrastructure services (DbContext, OpenIddict, etc.)
builder.Services.AddInfrastructureWithAspire(builder.Configuration, builder.Environment);

if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    builder.Services.AddConfiguredForwardedHeaders(builder.Configuration);
}

// Add API services with camelCase JSON serialization for frontend compatibility
#if !OPENIDENTITYSTACK_NATIVEAOT
builder.Services.AddControllersWithViews()
    .AddDefaultJsonOptions(ConfigureApiJsonOptions);
#endif
builder.Services.AddDefaultHttpJsonOptions(ConfigureApiJsonOptions); // Also configure Minimal API JSON serialization
#if !OPENIDENTITYSTACK_NATIVEAOT
builder.Services.AddRazorPages();
#endif
builder.Services.AddOpenApi();
bool disableRateLimiting = builder.Environment.IsEnvironment("Testing") || builder.Environment.IsDevelopment();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("InteractiveLogin", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartitionKey(httpContext, "login"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = disableRateLimiting ? int.MaxValue : 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    options.AddPolicy("TokenEndpoint", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartitionKey(httpContext, "token"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = disableRateLimiting ? int.MaxValue : 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        HttpContext httpContext = context.HttpContext;
        ProblemDetails problemDetails = context.ProblemDetails;
        
        // Get the exception if this is from exception handler middleware
        Exception? exception = context.Exception;
        
        // Special handling for cancellation exceptions - these are normal for OIDC flows
        // where the client closes the connection after receiving the token
        // Skip processing if response has already started or request was aborted
        if (exception is TaskCanceledException or OperationCanceledException)
        {
            if (httpContext.Response.HasStarted || httpContext.RequestAborted.IsCancellationRequested)
            {
                return;
            }
        }

        // Map exceptions to status codes and titles
        if (exception is not null && exception is not TaskCanceledException and not OperationCanceledException)
        {
            (int statusCode, string title) = exception switch
            {
                Microsoft.AspNetCore.Http.BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad Request"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
                InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
                _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
            };
            
            problemDetails.Status = statusCode;
            problemDetails.Title = title;
            httpContext.Response.StatusCode = statusCode;
        }

        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (exception is not null && exception.Data.Contains("ErrorCode"))
        {
            problemDetails.Extensions["errorCode"] = exception.Data["ErrorCode"];
        }

        if (exception is not null)
        {
            IHostEnvironment environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            if (environment.IsDevelopment())
            {
                problemDetails.Detail = exception.Message;
            }
            else
            {
                problemDetails.Detail = exception switch
                {
                    Microsoft.AspNetCore.Http.BadHttpRequestException => "The request body is invalid or malformed.",
                    ArgumentException => exception.Message,
                    UnauthorizedAccessException => "You are not authorized to perform this action.",
                    KeyNotFoundException => "The requested resource was not found.",
                    InvalidOperationException => exception.Message,
                    _ => "An unexpected error occurred. Please try again later."
                };
            }
        }
        
        // Set RFC 7231/7235 Type URIs for backward compatibility
        int currentStatusCode = problemDetails.Status ?? httpContext.Response.StatusCode;
        problemDetails.Type = currentStatusCode switch
        {
            StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
            StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            _ => problemDetails.Type ?? "https://tools.ietf.org/html/rfc7231#section-6.6.1" // Default to 500 error
        };
    };
});

builder.Services.AddDataProtection()
    .SetApplicationName("OpenIdentityStack")
    .PersistKeysToDbContext<OpenIdentityStackDbContext>();

builder.Services.AddScoped<IOpenIddictRequestService, OpenIddictRequestService>();

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

// T043: Configure CORS for Admin Web App
// In development/testing, allow dynamic origins (Aspire assigns random ports)
// In production, configure specific allowed origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminWeb", policy =>
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

// Enable CORS for Admin Web App
app.UseCors("AdminWeb");

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

// Map MVC Controllers for authentication endpoints (/connect/*, /Account/*)
// These handle OpenIddict OAuth2/OIDC flows and login UI
#if !OPENIDENTITYSTACK_NATIVEAOT
app.MapControllers();
#endif

// Map Minimal API endpoints
app.MapClientsApi();
app.MapUsersApi();
app.MapPublicProfilesApi();
app.MapRolesApi();
app.MapGroupsApi();
app.MapSessionsApi();
app.MapServiceAccountsApi();
app.MapServicePermissionsApi();
app.MapProvidersApi();
app.MapAuthenticationSettingsApi();

// Map Razor Pages for login UI
#if !OPENIDENTITYSTACK_NATIVEAOT
app.MapRazorPages();
#endif

// Map Aspire default endpoints (health, alive)
app.MapDefaultEndpoints();

app.MapGet("/", () => "OpenIdentityStack API is running")
    .WithName("Root");

await app.RunAsync();

static string GetClientPartitionKey(HttpContext httpContext, string suffix)
{
    string client = httpContext.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    return $"{suffix}:{client}";
}

static void ConfigureApiJsonOptions(JsonSerializerOptions options)
{
    options.TypeInfoResolverChain.Insert(0, OpenIdentityStackApiJsonContext.Default);
    options.Converters.Add(new JsonStringEnumConverter<ClientType>());
    options.Converters.Add(new JsonStringEnumConverter<DependencyImpact>());
    options.Converters.Add(new JsonStringEnumConverter<DependencyType>());
    options.Converters.Add(new JsonStringEnumConverter<LogoutStatus>());
    options.Converters.Add(new JsonStringEnumConverter<MappingType>());
    options.Converters.Add(new JsonStringEnumConverter<OwnerType>());
    options.Converters.Add(new JsonStringEnumConverter<PermissionLifecycleStatus>());
    options.Converters.Add(new JsonStringEnumConverter<ProviderStatus>());
    options.Converters.Add(new JsonStringEnumConverter<ServiceAccountStatus>());
    options.Converters.Add(new JsonStringEnumConverter<ServiceLifecycleStatus>());
    options.Converters.Add(new JsonStringEnumConverter<SessionStatus>());
    options.Converters.Add(new JsonStringEnumConverter<TokenTarget>());
    options.Converters.Add(new JsonStringEnumConverter<TransformType>());
    options.Converters.Add(new JsonStringEnumConverter<UserStatus>());
}

public partial class Program;
