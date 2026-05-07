using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Users;

/// <summary>
/// Tests for User status transitions.
/// </summary>
public sealed class UserStatusTransitionTests
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    public UserStatusTransitionTests()
    {
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);
    }

    #region VerifyEmail Tests

    [Fact]
    public void VerifyEmail_FromPendingVerification_TransitionsToActive()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "password", this._dateTimeProvider).Value;
        user.Status.ShouldBe(UserStatus.PendingVerification);

        // Act
        Result result = user.VerifyEmail(this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
    }

    [Fact]
    public void VerifyEmail_RaisesUserEmailVerifiedEvent()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "password", this._dateTimeProvider).Value;
        user.ClearDomainEvents();

        // Act
        user.VerifyEmail(this._dateTimeProvider);

        // Assert
        IReadOnlyList<DomainEvent> events = user.GetDomainEvents();
        events.ShouldContain(e => e is UserDomainEvents.UserEmailVerified);
    }

    [Fact]
    public void VerifyEmail_SetsModifiedAt()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "password", this._dateTimeProvider).Value;

        // Act
        user.VerifyEmail(this._dateTimeProvider);

        // Assert
        user.ModifiedAt.ShouldBe(this._now);
    }

    [Fact]
    public void VerifyEmail_FromActive_ReturnsFailure()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.Status.ShouldBe(UserStatus.Active);

        // Act
        Result result = user.VerifyEmail(this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("InvalidStatusTransition");
    }

    [Fact]
    public void VerifyEmail_FromDisabled_ReturnsFailure()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.Disable("Test", this._dateTimeProvider);

        // Act
        Result result = user.VerifyEmail(this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    #endregion

    #region Disable Tests

    [Fact]
    public void Disable_FromActive_TransitionsToDisabled()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.Status.ShouldBe(UserStatus.Active);

        // Act
        Result result = user.Disable("Security concern", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Disabled);
    }

    [Fact]
    public void Disable_RaisesUserDisabledEvent()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.ClearDomainEvents();

        // Act
        user.Disable("Security concern", this._dateTimeProvider);

        // Assert
        IReadOnlyList<DomainEvent> events = user.GetDomainEvents();
        UserDomainEvents.UserDisabled disabledEvent = events.OfType<UserDomainEvents.UserDisabled>().Single();
        disabledEvent.Reason.ShouldBe("Security concern");
    }

    [Fact]
    public void Disable_SetsModifiedAt()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;

        // Act
        user.Disable("Test", this._dateTimeProvider);

        // Assert
        user.ModifiedAt.ShouldBe(this._now);
    }

    [Fact]
    public void Disable_FromDisabled_ReturnsAlreadyDisabledError()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.Disable("First disable", this._dateTimeProvider);

        // Act
        Result result = user.Disable("Second disable", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.User.AlreadyDisabled");
    }

    [Fact]
    public void Disable_FromPendingVerification_ReturnsFailure()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "password", this._dateTimeProvider).Value;
        user.Status.ShouldBe(UserStatus.PendingVerification);

        // Act
        Result result = user.Disable("Test", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("InvalidStatusTransition");
    }

    #endregion

    #region Enable Tests

    [Fact]
    public void Enable_FromDisabled_TransitionsToActive()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.Disable("Test", this._dateTimeProvider);
        user.Status.ShouldBe(UserStatus.Disabled);

        // Act
        Result result = user.Enable(this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
    }

    [Fact]
    public void Enable_RaisesUserEnabledEvent()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.Disable("Test", this._dateTimeProvider);
        user.ClearDomainEvents();

        // Act
        user.Enable(this._dateTimeProvider);

        // Assert
        IReadOnlyList<DomainEvent> events = user.GetDomainEvents();
        events.ShouldContain(e => e is UserDomainEvents.UserEnabled);
    }

    [Fact]
    public void Enable_SetsModifiedAt()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.Disable("Test", this._dateTimeProvider);

        // Act
        user.Enable(this._dateTimeProvider);

        // Assert
        user.ModifiedAt.ShouldBe(this._now);
    }

    [Fact]
    public void Enable_FromActive_ReturnsNotDisabledError()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.Status.ShouldBe(UserStatus.Active);

        // Act
        Result result = user.Enable(this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.User.NotDisabled");
    }

    [Fact]
    public void Enable_FromPendingVerification_ReturnsNotDisabledError()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "password", this._dateTimeProvider).Value;

        // Act
        Result result = user.Enable(this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.User.NotDisabled");
    }

    #endregion

    #region Complete State Machine Tests

    [Fact]
    public void StateMachine_LocalUser_FullLifecycle()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "password", this._dateTimeProvider).Value;

        // Assert initial state
        user.Status.ShouldBe(UserStatus.PendingVerification);
        user.CanAuthenticate().ShouldBeFalse();

        // Verify email
        user.VerifyEmail(this._dateTimeProvider).IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
        user.CanAuthenticate().ShouldBeTrue();

        // Disable
        user.Disable("Test", this._dateTimeProvider).IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Disabled);
        user.CanAuthenticate().ShouldBeFalse();

        // Re-enable
        user.Enable(this._dateTimeProvider).IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
        user.CanAuthenticate().ShouldBeTrue();
    }

    [Fact]
    public void StateMachine_FederatedUser_FullLifecycle()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;

        // Assert initial state - federated users are immediately active
        user.Status.ShouldBe(UserStatus.Active);
        user.CanAuthenticate().ShouldBeTrue();

        // Disable
        user.Disable("Test", this._dateTimeProvider).IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Disabled);
        user.CanAuthenticate().ShouldBeFalse();

        // Re-enable
        user.Enable(this._dateTimeProvider).IsSuccess.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
        user.CanAuthenticate().ShouldBeTrue();
    }

    #endregion
}
