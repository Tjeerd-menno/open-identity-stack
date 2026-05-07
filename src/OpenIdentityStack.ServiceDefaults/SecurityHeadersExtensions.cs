using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for adding security headers middleware to web applications.
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds security headers middleware following OWASP recommendations.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">Optional configuration for security headers.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app,
        Action<SecurityHeadersOptions>? configure = null)
    {
        var options = new SecurityHeadersOptions();
        configure?.Invoke(options);

        return app.Use(async (context, next) =>
        {
            // Content Security Policy - restrict resources to same origin
            if (!string.IsNullOrEmpty(options.ContentSecurityPolicy))
            {
                context.Response.Headers["Content-Security-Policy"] = options.ContentSecurityPolicy;
            }

            // Prevent MIME type sniffing
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";

            // Prevent clickjacking
            context.Response.Headers["X-Frame-Options"] = options.FrameOptions;

            // Referrer policy
            context.Response.Headers["Referrer-Policy"] = options.ReferrerPolicy;

            // Permissions policy (restrict dangerous APIs)
            context.Response.Headers["Permissions-Policy"] = options.PermissionsPolicy;

            // Note: X-XSS-Protection is deprecated and removed in favor of CSP
            // Modern browsers have removed support for this header

            await next();
        });
    }
}

/// <summary>
/// Options for configuring security headers.
/// </summary>
public class SecurityHeadersOptions
{
    /// <summary>
    /// Gets or sets the Content-Security-Policy header value.
    /// Note: 'unsafe-inline' may be required for Scalar API docs to work correctly.
    /// For stricter security, consider using CSP nonces or hashes for inline scripts.
    /// Set to null to disable CSP header.
    /// </summary>
    public string? ContentSecurityPolicy { get; set; } =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self';";

    /// <summary>
    /// Gets or sets the X-Frame-Options header value. Default is "DENY".
    /// </summary>
    public string FrameOptions { get; set; } = "DENY";

    /// <summary>
    /// Gets or sets the Referrer-Policy header value.
    /// </summary>
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Gets or sets the Permissions-Policy header value.
    /// </summary>
    public string PermissionsPolicy { get; set; } = "geolocation=(), microphone=(), camera=()";
}
