using Microsoft.AspNetCore.Http;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Defaults and helpers for OpenID Connect session management.
/// </summary>
public static class SessionManagementDefaults
{
    /// <summary>
    /// Gets the cookie name used for session management.
    /// </summary>
    public const string SessionCookieName = "op_session";

    /// <summary>
    /// Creates cookie options for the session management cookie.
    /// </summary>
    public static CookieOptions CreateSessionCookieOptions(DateTimeOffset? expiresUtc = null)
    {
        return new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.None,
            Secure = true,
            Path = "/",
            Expires = expiresUtc
        };
    }
}
