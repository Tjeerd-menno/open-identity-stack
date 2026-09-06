using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
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
                RateLimitPartition.GetFixedWindowLimiter(
                    GetCheckSessionPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = environment.IsEnvironment("Testing") ? 3 : 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

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

    private static string GetCheckSessionPartitionKey(HttpContext httpContext)
    {
        string client = GetClientPartitionKey(httpContext, "check-session");
        if (!httpContext.Request.Cookies.TryGetValue(SessionManagementDefaults.SessionCookieName, out string? session)
            || string.IsNullOrWhiteSpace(session))
        {
            return client;
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(session));
        return $"{client}:{Convert.ToHexString(digest)}";
    }
}
