using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Infrastructure.Identity;

namespace OpenIdentityStack.Api.Configuration;

/// <summary>
/// Configures fixed-window rate limiting for interactive login, the token endpoint,
/// token introspection, and session monitoring. Authentication limits are effectively
/// disabled in Development and Testing; session monitoring remains bounded in every environment.
/// </summary>
public static class RateLimitingConfiguration
{
    public const string CheckSessionPolicy = "CheckSession";

    public static IServiceCollection AddConfiguredRateLimiting(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        bool disableRateLimiting = environment.IsEnvironment("Testing") || environment.IsDevelopment();

        services.AddRateLimiter(options =>
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

            options.AddPolicy(CheckSessionPolicy, httpContext =>
            {
                ISessionMonitoringCookieService cookies = httpContext.RequestServices
                    .GetRequiredService<ISessionMonitoringCookieService>();
                string? protectedSession = cookies.GetRateLimitPartitionKey(
                    httpContext.Request.Cookies[SessionManagementDefaults.SessionCookieName]);

                return RateLimitPartition.GetFixedWindowLimiter(
                    protectedSession is null
                        ? GetClientPartitionKey(httpContext, "check-session")
                        : $"check-session:protected:{protectedSession}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = protectedSession is null
                            ? environment.IsEnvironment("Testing") ? 3 : 300
                            : environment.IsEnvironment("Testing") ? 6 : 600,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/connect/introspect", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        GetClientPartitionKey(httpContext, "introspection"),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = disableRateLimiting ? int.MaxValue : 60,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                }

                if (httpContext.Request.Path.StartsWithSegments("/connect/check_session", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        GetClientPartitionKey(httpContext, "check-session-aggregate"),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = environment.IsEnvironment("Testing") ? 30 : 3000,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                }

                return RateLimitPartition.GetNoLimiter("default");
            });
        });

        return services;
    }

    private static string GetClientPartitionKey(HttpContext httpContext, string suffix)
    {
        string client = httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return $"{suffix}:{client}";
    }
}
