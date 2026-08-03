using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;

/// <summary>
/// Unit tests for the UpdateUserUseCase.
/// </summary>
public sealed class UpdateUserUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLog _auditLog;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);
    private readonly UpdateUserUseCase _sut;

    public UpdateUserUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._auditLog = Substitute.For<IAuditLog>();
        this._dateTimeProvider.UtcNow.Returns(this._now);

        this._sut = new UpdateUserUseCase(this._userRepository, this._dateTimeProvider, this._auditLog);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidDisplayName_ReturnsSuccess()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(userId, "New Name", "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidDisplayName_UpdatesDisplayName()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(userId, "New Name", "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        user.DisplayName.ShouldBe("New Name");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidDisplayName_SavesChanges()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(userId, "New Name", "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUpdatedAtFromDateTimeProvider()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(userId, "New Name", "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UpdatedAt.ShouldBe(this._now);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullDisplayName_SkipsDisplayNameUpdate()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Original Name");
        var command = new UpdateUserCommand(userId, null, "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.DisplayName.ShouldBe("Original Name");
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceDisplayName_SkipsDisplayNameUpdate()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Original Name");
        var command = new UpdateUserCommand(userId, "   ", "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.DisplayName.ShouldBe("Original Name");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_ReturnsNotFoundError()
    {
        // Arrange
        var userId = UserId.Create();
        var command = new UpdateUserCommand(userId, "New Name", "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_DoesNotSaveChanges()
    {
        // Arrange
        var userId = UserId.Create();
        var command = new UpdateUserCommand(userId, "New Name", "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._userRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithTooLongDisplayName_ReturnsValidationError()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        string tooLongName = new string('a', 257);
        var command = new UpdateUserCommand(userId, tooLongName, "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<UpdateUserResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.DisplayNameTooLong");
    }

    [Fact]
    public async Task ExecuteAsync_WithChangedPhoneNumberAndOmittedVerification_ResetsVerification()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateVerifiedPhoneUser(userId, "+31612345678");
        var command = new UpdateUserCommand(
            userId,
            null,
            "admin-1",
            new UserProfileData(PhoneNumber: "+32499999999"));

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        user.PhoneNumber.ShouldBe("+32499999999");
        user.PhoneNumberVerified.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithUnchangedPhoneNumberAndOmittedVerification_KeepsVerification()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateVerifiedPhoneUser(userId, "+31612345678");
        var command = new UpdateUserCommand(
            userId,
            null,
            "admin-1",
            new UserProfileData(GivenName: "Alice"));

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        user.PhoneNumber.ShouldBe("+31612345678");
        user.PhoneNumberVerified.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithClearedPhoneNumber_ResetsVerification()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateVerifiedPhoneUser(userId, "+31612345678");
        var command = new UpdateUserCommand(
            userId,
            null,
            "admin-1",
            new UserProfileData(PhoneNumber: "   "));

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        user.PhoneNumber.ShouldBeNull();
        user.PhoneNumberVerified.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithChangedPhoneNumberAndExplicitVerification_HonoursCaller()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateVerifiedPhoneUser(userId, "+31612345678");
        var command = new UpdateUserCommand(
            userId,
            null,
            "admin-1",
            new UserProfileData(PhoneNumber: "+32499999999", PhoneNumberVerified: true));

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        user.PhoneNumber.ShouldBe("+32499999999");
        user.PhoneNumberVerified.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithExplicitVerificationAndNoPhoneNumber_DoesNotAssertVerification()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(
            userId,
            null,
            "admin-1",
            new UserProfileData(PhoneNumberVerified: true));

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        user.PhoneNumber.ShouldBeNull();
        user.PhoneNumberVerified.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithExplicitVerificationAndBlankPhoneNumber_DoesNotAssertVerification()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateVerifiedPhoneUser(userId, "+31612345678");
        var command = new UpdateUserCommand(
            userId,
            null,
            "admin-1",
            new UserProfileData(PhoneNumber: "   ", PhoneNumberVerified: true));

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        user.PhoneNumber.ShouldBeNull();
        user.PhoneNumberVerified.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenVerificationIsAsserted_RecordsItInTheAuditTrail()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(
            userId,
            null,
            "admin-1",
            new UserProfileData(PhoneNumber: "+31612345678", PhoneNumberVerified: true));

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._auditLog.Received(1).LogAsync(
            "admin-1",
            "User.Updated",
            "User",
            userId.Value.ToString(),
            Arg.Is<string>(details =>
                details != null
                && details.Contains("PhoneNumberVerified: asserted", StringComparison.Ordinal)
                && !details.Contains("+31612345678", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenVerificationIsCleared_RecordsItInTheAuditTrail()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateVerifiedPhoneUser(userId, "+31612345678");
        var command = new UpdateUserCommand(
            userId,
            null,
            "admin-1",
            new UserProfileData(PhoneNumber: "+32499999999"));

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._auditLog.Received(1).LogAsync(
            "admin-1",
            "User.Updated",
            "User",
            userId.Value.ToString(),
            Arg.Is<string>(details =>
                details != null && details.Contains("PhoneNumberVerified: cleared", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenVerificationIsUnchanged_LeavesTheAuditDetailsAlone()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateUser(userId, "Old Name");
        var command = new UpdateUserCommand(userId, "New Name", "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        await this._auditLog.Received(1).LogAsync(
            "admin-1",
            "User.Updated",
            "User",
            userId.Value.ToString(),
            Arg.Is<string>(details =>
                details != null && !details.Contains("PhoneNumberVerified", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    private User CreateVerifiedPhoneUser(UserId userId, string phoneNumber)
    {
        User user = CreateUser(userId, "Old Name");

        user.UpdateProfile(
            new UserProfileData(PhoneNumber: phoneNumber, PhoneNumberVerified: true),
            this._dateTimeProvider);

        return user;
    }

    private User CreateUser(UserId userId, string displayName)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(this._now);

        Result<User> result = User.CreateLocal(
            "test@example.com",
            displayName,
            "hashedPassword",
            dateTimeProvider);

        User user = result.Value;

        typeof(User).BaseType!.BaseType!
            .GetProperty("Id")!
            .SetValue(user, userId);

        return user;
    }
}
