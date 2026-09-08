using Microsoft.AspNetCore.Http;
using OpenIdentityStack.Infrastructure.Identity;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class SessionManagementDefaultsTests
{
    [Fact]
    public void SessionCookieName_IsCorrectConstant()
    {
        // Assert
        SessionManagementDefaults.SessionCookieName.ShouldBe("op_session");
    }

    [Fact]
    public void CreateSessionCookieOptions_SetsCorrectDefaults()
    {
        // Act
        CookieOptions options = SessionManagementDefaults.CreateSessionCookieOptions();

        // Assert
        options.HttpOnly.ShouldBeFalse("HttpOnly must be false for client-side access");
        options.SameSite.ShouldBe(SameSiteMode.None, "SameSite must be None for cross-origin requests");
        options.Secure.ShouldBeTrue("Secure must be true for SameSite=None");
        options.Path.ShouldBe("/");
        options.Expires.ShouldBeNull();
    }

    [Fact]
    public void CreateSessionCookieOptions_SetsExpiresWhenProvided()
    {
        // Arrange
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        CookieOptions options = SessionManagementDefaults.CreateSessionCookieOptions(expires);

        // Assert
        options.Expires.ShouldBe(expires);
    }

    [Fact]
    public void CreateSessionCookieOptions_HasCorrectPathForAllEndpoints()
    {
        // Act
        CookieOptions options = SessionManagementDefaults.CreateSessionCookieOptions();

        // Assert
        options.Path.ShouldBe("/", "Cookie should be accessible from all paths");
    }
}
