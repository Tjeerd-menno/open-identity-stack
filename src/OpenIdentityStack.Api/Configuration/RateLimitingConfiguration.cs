using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;

namespace OpenIdentityStack.Api.Configuration;

/// <summary>
/// Configures fixed-window rate limiting for interactive login, the token endpoint,
/// and token introspection. Limits are effectively disabled in Development and Testing.
/// </summary>
public static class RateLimitingConfiguration
{
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
}
