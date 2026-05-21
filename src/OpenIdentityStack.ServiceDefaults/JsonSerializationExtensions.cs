using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring JSON serialization options.
/// </summary>
public static class JsonSerializationExtensions
{
    /// <summary>
    /// Configures default JSON serialization options for MVC controllers.
    /// </summary>
    /// <param name="builder">The MVC builder.</param>
    /// <returns>The MVC builder for chaining.</returns>
    public static IMvcBuilder AddDefaultJsonOptions(
        this IMvcBuilder builder,
        Action<JsonSerializerOptions>? configure = null)
    {
        return builder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            configure?.Invoke(options.JsonSerializerOptions);
        });
    }

    /// <summary>
    /// Configures default JSON serialization options for Minimal APIs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDefaultHttpJsonOptions(
        this IServiceCollection services,
        Action<JsonSerializerOptions>? configure = null)
    {
        return services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            configure?.Invoke(options.SerializerOptions);
        });
    }
}
