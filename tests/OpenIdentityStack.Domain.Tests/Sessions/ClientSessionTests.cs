using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Sessions;
/// <summary>
/// Unit tests for ClientSession entity.
/// </summary>
public sealed class ClientSessionTests
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

    public ClientSessionTests()
    {
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);
    }

    #region Create Tests

    [Fact]
    public void Create_WithValidClientId_ReturnsSuccess()
    {
        // Act
        Result<ClientSession> result = ClientSession.Create("client-app", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientId.ShouldBe("client-app");
    }

    [Fact]
    public void Create_SetsAuthenticatedAtToCurrentTime()
    {
        // Act
        Result<ClientSession> result = ClientSession.Create("client-app", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AuthenticatedAt.ShouldBe(this._now);
    }

    [Fact]
    public void Create_SetsLastAccessAtToCurrentTime()
    {
        // Act
        Result<ClientSession> result = ClientSession.Create("client-app", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.LastAccessAt.ShouldBe(this._now);
    }

    [Fact]
    public void Create_WithEmptyClientId_ReturnsError()
    {
        // Act
        Result<ClientSession> result = ClientSession.Create("", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SessionErrors.ClientIdRequired);
    }

    [Fact]
    public void Create_WithWhitespaceClientId_ReturnsError()
    {
        // Act
        Result<ClientSession> result = ClientSession.Create("   ", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SessionErrors.ClientIdRequired);
    }

    [Fact]
    public void Create_WithNullClientId_ReturnsError()
    {
        // Act
        Result<ClientSession> result = ClientSession.Create(null!, this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SessionErrors.ClientIdRequired);
    }

    [Fact]
    public void Create_InitializesLogoutUrisAsNull()
    {
        // Act
        Result<ClientSession> result = ClientSession.Create("client-app", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.FrontChannelLogoutUri.ShouldBeNull();
        result.Value.BackChannelLogoutUri.ShouldBeNull();
    }

    #endregion

    #region UpdateLastAccess Tests

    [Fact]
    public void UpdateLastAccess_UpdatesLastAccessAt()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        DateTimeOffset laterTime = this._now.AddHours(1);
        IDateTimeProvider laterProvider = Substitute.For<IDateTimeProvider>();
        laterProvider.UtcNow.Returns(laterTime);

        // Act
        session.UpdateLastAccess(laterProvider);

        // Assert
        session.LastAccessAt.ShouldBe(laterTime);
    }

    [Fact]
    public void UpdateLastAccess_DoesNotAffectAuthenticatedAt()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        DateTimeOffset originalAuthenticatedAt = session.AuthenticatedAt;
        IDateTimeProvider laterProvider = Substitute.For<IDateTimeProvider>();
        laterProvider.UtcNow.Returns(this._now.AddHours(1));

        // Act
        session.UpdateLastAccess(laterProvider);

        // Assert
        session.AuthenticatedAt.ShouldBe(originalAuthenticatedAt);
    }

    #endregion

    #region SetLogoutUris Tests

    [Fact]
    public void SetLogoutUris_SetsBothUris()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act
        session.SetLogoutUris("https://app.com/logout", "https://app.com/backchannel-logout");

        // Assert
        session.FrontChannelLogoutUri.ShouldBe("https://app.com/logout");
        session.BackChannelLogoutUri.ShouldBe("https://app.com/backchannel-logout");
    }

    [Fact]
    public void SetLogoutUris_AllowsNullValues()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        session.SetLogoutUris("https://app.com/logout", "https://app.com/backchannel");

        // Act
        session.SetLogoutUris(null, null);

        // Assert
        session.FrontChannelLogoutUri.ShouldBeNull();
        session.BackChannelLogoutUri.ShouldBeNull();
    }

    [Fact]
    public void SetLogoutUris_CanSetOnlyFrontChannel()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act
        session.SetLogoutUris("https://app.com/logout", null);

        // Assert
        session.FrontChannelLogoutUri.ShouldBe("https://app.com/logout");
        session.BackChannelLogoutUri.ShouldBeNull();
    }

    [Fact]
    public void SetLogoutUris_CanSetOnlyBackChannel()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act
        session.SetLogoutUris(null, "https://app.com/backchannel-logout");

        // Assert
        session.FrontChannelLogoutUri.ShouldBeNull();
        session.BackChannelLogoutUri.ShouldBe("https://app.com/backchannel-logout");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_SameClientId_ReturnsTrue()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        ClientSession session2 = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act & Assert
        session1.Equals(session2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentClientId_ReturnsFalse()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app-1", this._dateTimeProvider).Value;
        ClientSession session2 = ClientSession.Create("client-app-2", this._dateTimeProvider).Value;

        // Act & Assert
        session1.Equals(session2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_NullSession_ReturnsFalse()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        ClientSession? nullSession = null;

        // Act & Assert
        session.Equals(nullSession).ShouldBeFalse();
    }

    [Fact]
    public void Equals_SameReference_ReturnsTrue()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act & Assert
        session.Equals(session).ShouldBeTrue();
    }

    [Fact]
    public void Equals_ObjectOverload_SameClientId_ReturnsTrue()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        object session2 = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act & Assert
        session1.Equals(session2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_ObjectOverload_NullObject_ReturnsFalse()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        object? nullObj = null;

        // Act & Assert
        session.Equals(nullObj).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_DifferentType_ReturnsFalse()
    {
        // Arrange
        ClientSession session = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        object differentType = "not a session";

        // Act & Assert
        session.Equals(differentType).ShouldBeFalse();
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_SameClientId_ReturnsSameHash()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        ClientSession session2 = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act & Assert
        session1.GetHashCode().ShouldBe(session2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentClientId_ReturnsDifferentHash()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app-1", this._dateTimeProvider).Value;
        ClientSession session2 = ClientSession.Create("client-app-2", this._dateTimeProvider).Value;

        // Act & Assert
        session1.GetHashCode().ShouldNotBe(session2.GetHashCode());
    }

    #endregion

    #region Operator Tests

    [Fact]
    public void EqualityOperator_SameClientId_ReturnsTrue()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        ClientSession session2 = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act & Assert
        (session1 == session2).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_DifferentClientId_ReturnsFalse()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app-1", this._dateTimeProvider).Value;
        ClientSession session2 = ClientSession.Create("client-app-2", this._dateTimeProvider).Value;

        // Act & Assert
        (session1 == session2).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_LeftNull_ReturnsFalse()
    {
        // Arrange
        ClientSession? session1 = null;
        ClientSession session2 = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act & Assert
        (session1 == session2).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_RightNull_ReturnsFalse()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        ClientSession? session2 = null;

        // Act & Assert
        (session1 == session2).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_BothNull_ReturnsTrue()
    {
        // Arrange
        ClientSession? session1 = null;
        ClientSession? session2 = null;

        // Act & Assert
        (session1 == session2).ShouldBeTrue();
    }

    [Fact]
    public void InequalityOperator_SameClientId_ReturnsFalse()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app", this._dateTimeProvider).Value;
        ClientSession session2 = ClientSession.Create("client-app", this._dateTimeProvider).Value;

        // Act & Assert
        (session1 != session2).ShouldBeFalse();
    }

    [Fact]
    public void InequalityOperator_DifferentClientId_ReturnsTrue()
    {
        // Arrange
        ClientSession session1 = ClientSession.Create("client-app-1", this._dateTimeProvider).Value;
        ClientSession session2 = ClientSession.Create("client-app-2", this._dateTimeProvider).Value;

        // Act & Assert
        (session1 != session2).ShouldBeTrue();
    }

    #endregion
}
