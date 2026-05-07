
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Sessions;
/// <summary>
/// Unit tests for the UserSession aggregate root.
/// </summary>
public sealed class UserSessionTests
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public UserSessionTests()
    {
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
    }

    [Fact]
    public void Create_WithValidData_ReturnsSession()
    {
        // Arrange
        var userId = UserId.Create();
        string ipAddress = "192.168.1.100";
        string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        // Act
        Result<UserSession> result = UserSession.Create(
            userId,
            ipAddress,
            userAgent,
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(userId);
        result.Value.IpAddress.ShouldBe(ipAddress);
        result.Value.UserAgent.ShouldBe(userAgent);
        result.Value.Status.ShouldBe(SessionStatus.Active);
        result.Value.CreatedAt.ShouldBe(TestTime);
    }

    [Fact]
    public void Create_WithEmptyUserId_ReturnsError()
    {
        // Arrange
        var userId = new UserId(Guid.Empty);
        string ipAddress = "192.168.1.100";
        string userAgent = "Mozilla/5.0";

        // Act
        Result<UserSession> result = UserSession.Create(
            userId,
            ipAddress,
            userAgent,
            this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Session.UserIdRequired");
    }

    [Fact]
    public void Create_InitializesAsActive()
    {
        // Arrange
        var userId = UserId.Create();

        // Act
        Result<UserSession> result = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(SessionStatus.Active);
        result.Value.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void Revoke_ActiveSession_MarksAsRevoked()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        DateTimeOffset revokeTime = TestTime.AddHours(1);
        this._dateTimeProvider.UtcNow.Returns(revokeTime);

        // Act
        Result result = session.Revoke(this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.Status.ShouldBe(SessionStatus.Revoked);
        session.RevokedAt.ShouldBe(revokeTime);
    }

    [Fact]
    public void Revoke_AlreadyRevokedSession_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        session.Revoke(this._dateTimeProvider);

        // Act
        Result result = session.Revoke(this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Session.AlreadyRevoked");
    }

    [Fact]
    public void Revoke_ExpiredSession_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        // Simulate session expiry
        session.Expire(this._dateTimeProvider);

        // Act
        Result result = session.Revoke(this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Session.AlreadyExpired");
    }

    [Fact]
    public void AddClientSession_WithValidData_AddsToSession()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        string clientId = "my-client-app";

        // Act
        Result result = session.AddClientSession(clientId, this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.ClientSessions.ShouldHaveSingleItem();
        session.ClientSessions[0].ClientId.ShouldBe(clientId);
    }

    [Fact]
    public void AddClientSession_ToRevokedSession_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        session.Revoke(this._dateTimeProvider);

        // Act
        Result result = session.AddClientSession("my-client-app", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Session.SessionNotActive");
    }

    [Fact]
    public void UpdateLastActivity_UpdatesTimestamp()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        DateTimeOffset activityTime = TestTime.AddMinutes(30);
        this._dateTimeProvider.UtcNow.Returns(activityTime);

        // Act
        session.UpdateLastActivity(this._dateTimeProvider);

        // Assert
        session.LastActivityAt.ShouldBe(activityTime);
    }

    [Fact]
    public void IsExpired_WhenPastExpirationTime_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider,
            TimeSpan.FromHours(1)).Value;

        // Simulate time passing beyond expiration
        DateTimeOffset laterTime = TestTime.AddHours(2);
        this._dateTimeProvider.UtcNow.Returns(laterTime);

        // Act
        bool isExpired = session.IsExpired(this._dateTimeProvider);

        // Assert
        isExpired.ShouldBeTrue();
    }

    [Fact]
    public void IsExpired_WhenBeforeExpirationTime_ReturnsFalse()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider,
            TimeSpan.FromHours(1)).Value;

        // Still within expiration window
        DateTimeOffset laterTime = TestTime.AddMinutes(30);
        this._dateTimeProvider.UtcNow.Returns(laterTime);

        // Act
        bool isExpired = session.IsExpired(this._dateTimeProvider);

        // Assert
        isExpired.ShouldBeFalse();
    }

    [Fact]
    public void Logout_ActiveSession_MarksAsLoggedOut()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        // Act
        Result result = session.Logout(this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        session.Status.ShouldBe(SessionStatus.LoggedOut);
    }

    [Fact]
    public void Logout_AlreadyLoggedOutSession_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        session.Logout(this._dateTimeProvider);

        // Act
        Result result = session.Logout(this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Session.AlreadyLoggedOut");
    }

    [Fact]
    public void Logout_RevokedSession_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        session.Revoke(this._dateTimeProvider);

        // Act
        Result result = session.Logout(this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Session.AlreadyRevoked");
    }

    [Fact]
    public void Logout_ExpiredSession_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        UserSession session = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider).Value;

        session.Expire(this._dateTimeProvider);

        // Act
        Result result = session.Logout(this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Session.AlreadyExpired");
    }

    [Fact]
    public void Create_SetsLastActivityAtToCreationTime()
    {
        // Arrange
        var userId = UserId.Create();

        // Act
        Result<UserSession> result = UserSession.Create(
            userId,
            "192.168.1.100",
            "Mozilla/5.0",
            this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.LastActivityAt.ShouldBe(TestTime);
    }
}
