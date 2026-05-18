using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Users;

/// <summary>
/// Tests for User entity creation and validation.
/// </summary>
public sealed class UserTests
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    public UserTests()
    {
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);
    }

    #region CreateLocal Tests

    [Fact]
    public void CreateLocal_WithValidData_ReturnsSuccessWithUser()
    {
        // Arrange
        string email = "test@example.com";
        string displayName = "Test User";
        string passwordHash = "hashed_password_123";

        // Act
        Result<User> result = User.CreateLocal(email, displayName, passwordHash, this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe(email);
        result.Value.DisplayName.ShouldBe(displayName);
        result.Value.PasswordHash.ShouldBe(passwordHash);
        result.Value.Status.ShouldBe(UserStatus.PendingVerification);
    }

    [Fact]
    public void CreateLocal_WithValidData_SetsNormalizedEmail()
    {
        // Arrange
        string email = "Test@Example.COM";

        // Act
        Result<User> result = User.CreateLocal(email, "Test", "password", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.NormalizedEmail.ShouldBe("TEST@EXAMPLE.COM");
    }

    [Fact]
    public void CreateLocal_WithValidData_SetsCreatedAt()
    {
        // Act
        Result<User> result = User.CreateLocal("test@example.com", "Test", "password", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CreatedAt.ShouldBe(this._now);
    }

    [Fact]
    public void CreateLocal_WithValidData_RaisesUserCreatedEvent()
    {
        // Act
        Result<User> result = User.CreateLocal("test@example.com", "Test User", "password", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        IReadOnlyList<DomainEvent> domainEvents = result.Value.GetDomainEvents();
        domainEvents.ShouldContain(e => e is UserDomainEvents.UserCreated);

        UserDomainEvents.UserCreated createdEvent = domainEvents.OfType<UserDomainEvents.UserCreated>().Single();
        createdEvent.UserId.ShouldBe(result.Value.Id);
        createdEvent.Email.ShouldBe("test@example.com");
        createdEvent.DisplayName.ShouldBe("Test User");
    }

    [Fact]
    public void CreateLocal_WithValidData_GeneratesUniqueId()
    {
        // Act
        Result<User> result1 = User.CreateLocal("test1@example.com", "Test", "password", this._dateTimeProvider);
        Result<User> result2 = User.CreateLocal("test2@example.com", "Test", "password", this._dateTimeProvider);

        // Assert
        result1.Value.Id.ShouldNotBe(result2.Value.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateLocal_WithEmptyEmail_ReturnsFailure(string? email)
    {
        // Act
        Result<User> result = User.CreateLocal(email!, "Test", "password", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.EmailRequired");
    }

    [Fact]
    public void CreateLocal_WithEmailTooLong_ReturnsFailure()
    {
        // Arrange
        string email = new string('a', 250) + "@test.com";

        // Act
        Result<User> result = User.CreateLocal(email, "Test", "password", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.EmailTooLong");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@domain")]
    [InlineData("@nodomain.com")]
    [InlineData("spaces in@email.com")]
    public void CreateLocal_WithInvalidEmailFormat_ReturnsFailure(string email)
    {
        // Act
        Result<User> result = User.CreateLocal(email, "Test", "password", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.EmailInvalidFormat");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateLocal_WithEmptyDisplayName_ReturnsFailure(string? displayName)
    {
        // Act
        Result<User> result = User.CreateLocal("test@example.com", displayName!, "password", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.DisplayNameRequired");
    }

    [Fact]
    public void CreateLocal_WithDisplayNameTooLong_ReturnsFailure()
    {
        // Arrange
        string displayName = new string('a', 257);

        // Act
        Result<User> result = User.CreateLocal("test@example.com", displayName, "password", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.DisplayNameTooLong");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateLocal_WithEmptyPassword_ReturnsFailure(string? passwordHash)
    {
        // Act
        Result<User> result = User.CreateLocal("test@example.com", "Test", passwordHash!, this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.PasswordRequired");
    }

    [Fact]
    public void CreateLocal_TrimsEmailAndDisplayName()
    {
        // Act
        Result<User> result = User.CreateLocal("  test@example.com  ", "  Test User  ", "password", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("test@example.com");
        result.Value.DisplayName.ShouldBe("Test User");
    }

    [Fact]
    public void CreateLocal_WithProfileData_SetsAndTrimsProfileFields()
    {
        // Arrange
        UserProfileData profile = new(
            GivenName: "  Alice  ",
            FamilyName: "  Example ",
            PreferredUsername: " alice.example ",
            Profile: " https://profiles.example.test/alice ",
            Picture: " https://profiles.example.test/alice/avatar.svg ",
            Website: " https://profiles.example.test/alice ",
            Gender: " female ",
            Birthdate: " 1990-01-15 ",
            ZoneInfo: " Europe/Amsterdam ",
            Locale: " en-NL ");

        // Act
        Result<User> result = User.CreateLocal("alice@example.test", "Alice Example", "password", this._dateTimeProvider, profile);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.GivenName.ShouldBe("Alice");
        result.Value.FamilyName.ShouldBe("Example");
        result.Value.PreferredUsername.ShouldBe("alice.example");
        result.Value.Profile.ShouldBe("https://profiles.example.test/alice");
        result.Value.Picture.ShouldBe("https://profiles.example.test/alice/avatar.svg");
        result.Value.Website.ShouldBe("https://profiles.example.test/alice");
        result.Value.Gender.ShouldBe("female");
        result.Value.Birthdate.ShouldBe("1990-01-15");
        result.Value.ZoneInfo.ShouldBe("Europe/Amsterdam");
        result.Value.Locale.ShouldBe("en-NL");
    }

    [Theory]
    [InlineData("alice example", null, "Validation.User.PreferredUsernameInvalid")]
    [InlineData(null, "/profiles/alice", "Validation.User.ProfileInvalid")]
    [InlineData(null, "javascript:alert('xss')", "Validation.User.ProfileInvalid")]
    [InlineData(null, "data:text/html,<script>alert('xss')</script>", "Validation.User.ProfileInvalid")]
    public void CreateLocal_WithInvalidProfileData_ReturnsFailure(
        string? invalidPreferredUsername,
        string? invalidProfileUri,
        string expectedErrorCode)
    {
        // Arrange
        UserProfileData profile = new(
            PreferredUsername: invalidPreferredUsername,
            Profile: invalidProfileUri);

        // Act
        Result<User> result = User.CreateLocal("alice@example.test", "Alice Example", "password", this._dateTimeProvider, profile);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(expectedErrorCode);
    }

    #endregion

    #region CreateFederated Tests

    [Fact]
    public void CreateFederated_WithValidData_ReturnsSuccessWithUser()
    {
        // Arrange
        string email = "federated@example.com";
        string displayName = "Federated User";

        // Act
        Result<User> result = User.CreateFederated(email, displayName, this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe(email);
        result.Value.DisplayName.ShouldBe(displayName);
        result.Value.PasswordHash.ShouldBeNull();
        result.Value.Status.ShouldBe(UserStatus.Active); // Federated users are immediately active
    }

    [Fact]
    public void CreateFederated_DoesNotRequirePassword()
    {
        // Act
        Result<User> result = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.HasPassword().ShouldBeFalse();
    }

    #endregion

    #region HasPassword and CanAuthenticate Tests

    [Fact]
    public void HasPassword_WithLocalUser_ReturnsTrue()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "password", this._dateTimeProvider).Value;

        // Assert
        user.HasPassword().ShouldBeTrue();
    }

    [Fact]
    public void HasPassword_WithFederatedUser_ReturnsFalse()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;

        // Assert
        user.HasPassword().ShouldBeFalse();
    }

    [Fact]
    public void CanAuthenticate_WithActiveUser_ReturnsTrue()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;

        // Assert
        user.CanAuthenticate().ShouldBeTrue();
    }

    [Fact]
    public void CanAuthenticate_WithPendingVerificationUser_ReturnsFalse()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "password", this._dateTimeProvider).Value;

        // Assert
        user.CanAuthenticate().ShouldBeFalse();
    }

    [Fact]
    public void CanAuthenticate_WithDisabledUser_ReturnsFalse()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.Disable("Test reason", this._dateTimeProvider);

        // Assert
        user.CanAuthenticate().ShouldBeFalse();
    }

    #endregion

    #region RecordLogin Tests

    [Fact]
    public void RecordLogin_UpdatesLastLoginAt()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.LastLoginAt.ShouldBeNull();

        // Act
        user.RecordLogin(this._dateTimeProvider);

        // Assert
        user.LastLoginAt.ShouldBe(this._now);
    }

    [Fact]
    public void RecordLogin_RaisesUserLoggedInEvent()
    {
        // Arrange
        User user = User.CreateFederated("test@example.com", "Test", this._dateTimeProvider).Value;
        user.ClearDomainEvents();

        // Act
        user.RecordLogin(this._dateTimeProvider);

        // Assert
        IReadOnlyList<DomainEvent> events = user.GetDomainEvents();
        events.ShouldContain(e => e is UserDomainEvents.UserLoggedIn);
    }

    #endregion

    #region SetPassword Tests

    [Fact]
    public void SetPassword_WithValidHash_UpdatesPassword()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "old_hash", this._dateTimeProvider).Value;

        // Act
        Result result = user.SetPassword("new_hash", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe("new_hash");
    }

    [Fact]
    public void SetPassword_WithEmptyHash_ReturnsFailure()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "old_hash", this._dateTimeProvider).Value;

        // Act
        Result result = user.SetPassword("", this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.PasswordRequired");
    }

    [Fact]
    public void SetPassword_RaisesPasswordChangedEvent()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Test", "old_hash", this._dateTimeProvider).Value;
        user.ClearDomainEvents();

        // Act
        user.SetPassword("new_hash", this._dateTimeProvider);

        // Assert
        IReadOnlyList<DomainEvent> events = user.GetDomainEvents();
        events.ShouldContain(e => e is UserDomainEvents.UserPasswordChanged);
    }

    #endregion

    #region UpdateDisplayName Tests

    [Fact]
    public void UpdateDisplayName_WithValidName_UpdatesName()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Old Name", "password", this._dateTimeProvider).Value;

        // Act
        Result result = user.UpdateDisplayName("New Name", this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.DisplayName.ShouldBe("New Name");
    }

    [Fact]
    public void UpdateDisplayName_TrimsName()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Old", "password", this._dateTimeProvider).Value;

        // Act
        user.UpdateDisplayName("  New Name  ", this._dateTimeProvider);

        // Assert
        user.DisplayName.ShouldBe("New Name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayName_WithEmptyName_ReturnsFailure(string? displayName)
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Old", "password", this._dateTimeProvider).Value;

        // Act
        Result result = user.UpdateDisplayName(displayName!, this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.DisplayNameRequired");
    }

    [Fact]
    public void UpdateDisplayName_WithNameTooLong_ReturnsFailure()
    {
        // Arrange
        User user = User.CreateLocal("test@example.com", "Old", "password", this._dateTimeProvider).Value;

        // Act
        Result result = user.UpdateDisplayName(new string('a', 257), this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.DisplayNameTooLong");
    }

    [Fact]
    public void UpdateProfile_WithValidValues_UpdatesProfileAndModifiedAt()
    {
        // Arrange
        User user = User.CreateLocal("alice@example.test", "Alice Example", "password", this._dateTimeProvider).Value;
        UserProfileData profile = new(
            GivenName: "Alice",
            FamilyName: "Example",
            PreferredUsername: "alice.example",
            Locale: "en-NL");

        // Act
        Result result = user.UpdateProfile(profile, this._dateTimeProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.GivenName.ShouldBe("Alice");
        user.FamilyName.ShouldBe("Example");
        user.PreferredUsername.ShouldBe("alice.example");
        user.Locale.ShouldBe("en-NL");
        user.ModifiedAt.ShouldBe(this._now);
    }

    #endregion
}
