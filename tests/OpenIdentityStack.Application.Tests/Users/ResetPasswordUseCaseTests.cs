using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;
/// <summary>
/// Unit tests for the ResetPasswordUseCase.
/// </summary>
public sealed class ResetPasswordUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyValidator _passwordPolicyValidator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IResetPasswordUseCase _sut;

    public ResetPasswordUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._passwordHasher = Substitute.For<IPasswordHasher>();
        this._passwordPolicyValidator = Substitute.For<IPasswordPolicyValidator>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        this._passwordHasher.HashPassword(Arg.Any<string>()).Returns(callInfo => $"hashed_{callInfo.Arg<string>()}");

        // Setup password validation to mimic real validator behavior
        this._passwordPolicyValidator.ValidatePassword(Arg.Any<string>()).Returns(callInfo =>
        {
            string password = callInfo.Arg<string>();
            if (string.IsNullOrWhiteSpace(password))
            {
                return DomainError.Validation("Password.Required", "Password is required.");
            }
            return Result.Success();
        });

        this._sut = new ResetPasswordUseCase(this._userRepository, this._passwordHasher, this._passwordPolicyValidator, this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidUser_ResetsPassword()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateActiveUser(userId);
        string newPassword = "NewSecurePassword123!";
        var command = new ResetPasswordCommand(userId, newPassword);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ResetPasswordResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe($"hashed_{newPassword}");
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_ReturnsNotFoundError()
    {
        // Arrange
        var userId = UserId.Create();
        var command = new ResetPasswordCommand(userId, "NewPassword");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<ResetPasswordResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.User.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPassword_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateActiveUser(userId);
        var command = new ResetPasswordCommand(userId, string.Empty);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ResetPasswordResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.Required");
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespacePassword_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateActiveUser(userId);
        var command = new ResetPasswordCommand(userId, "   ");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ResetPasswordResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.Required");
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledUser_ResetsPassword()
    {
        // Arrange - Disabled users should still be able to have their password reset by admin
        var userId = UserId.Create();
        User user = CreateDisabledUser(userId);
        string newPassword = "NewSecurePassword123!";
        var command = new ResetPasswordCommand(userId, newPassword);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ResetPasswordResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe($"hashed_{newPassword}");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsResetAtTimestamp()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateActiveUser(userId);
        var command = new ResetPasswordCommand(userId, "NewPassword");
        DateTimeOffset expectedTime = DateTimeOffset.UtcNow;
        this._dateTimeProvider.UtcNow.Returns(expectedTime);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ResetPasswordResult> result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ResetAt.ShouldBe(expectedTime);
    }

    [Fact]
    public async Task ExecuteAsync_CallsPasswordHasher()
    {
        // Arrange
        var userId = UserId.Create();
        User user = CreateActiveUser(userId);
        string newPassword = "NewPassword123!";
        var command = new ResetPasswordCommand(userId, newPassword);

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await this._sut.ExecuteAsync(command);

        // Assert
        this._passwordHasher.Received(1).HashPassword(newPassword);
    }

    private static User CreateActiveUser(UserId userId)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        Result<User> result = User.CreateLocal(
            "test@example.com",
            "Test User",
            "originalHashedPassword",
            dateTimeProvider);

        User user = result.Value;
        user.VerifyEmail(dateTimeProvider);

        // Use reflection to set the Id for testing
        typeof(User).BaseType!.BaseType!
            .GetProperty("Id")!
            .SetValue(user, userId);

        return user;
    }

    private static User CreateDisabledUser(UserId userId)
    {
        User user = CreateActiveUser(userId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        user.Disable("Disabled for testing", dateTimeProvider);
        return user;
    }
}
