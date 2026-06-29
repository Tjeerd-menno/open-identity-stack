using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;

/// <summary>
/// Tests for CreateUserUseCase.
/// </summary>
public sealed class CreateUserUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyValidator _passwordPolicyValidator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLog _auditLog;
    private readonly ILogger<CreateUserUseCase> _logger;
    private readonly CreateUserUseCase _useCase;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    public CreateUserUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._passwordHasher = Substitute.For<IPasswordHasher>();
        this._passwordPolicyValidator = Substitute.For<IPasswordPolicyValidator>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._auditLog = Substitute.For<IAuditLog>();
        this._logger = Substitute.For<ILogger<CreateUserUseCase>>();
        this._dateTimeProvider.UtcNow.Returns(this._now);
        this._passwordHasher.HashPassword(Arg.Any<string>()).Returns(ci => $"hashed_{ci.Arg<string>()}");

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

        this._useCase = new CreateUserUseCase(this._userRepository, this._passwordHasher, this._passwordPolicyValidator, this._dateTimeProvider, this._auditLog, this._logger);
    }

    #region Success Cases

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_ReturnsSuccess()
    {
        // Arrange
        var command = new CreateUserCommand("test@example.com", "Test User", "Password123!", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateUserResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("test@example.com");
        result.Value.DisplayName.ShouldBe("Test User");
        result.Value.UserId.ShouldNotBe(UserId.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_HashesPassword()
    {
        // Arrange
        var command = new CreateUserCommand("test@example.com", "Test User", "MyPassword", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        this._passwordHasher.Received(1).HashPassword("MyPassword");
    }

    [Fact]
    public async Task ExecuteAsync_AddsUserToRepository()
    {
        // Arrange
        var command = new CreateUserCommand("test@example.com", "Test User", "Password123!", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == "test@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SavesChanges()
    {
        // Arrange
        var command = new CreateUserCommand("test@example.com", "Test User", "Password123!", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithProfile_PersistsProfileFields()
    {
        // Arrange
        var command = new CreateUserCommand(
            "alice@example.test",
            "Alice Example",
            "Password123!",
            "admin-1",
            new UserProfileData(
                GivenName: "Alice",
                FamilyName: "Example",
                PreferredUsername: "alice.example",
                Locale: "en-NL"));
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateUserResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await this._userRepository.Received(1).AddAsync(
            Arg.Is<User>(user =>
                user.GivenName == "Alice"
                && user.FamilyName == "Example"
                && user.PreferredUsername == "alice.example"
                && user.Locale == "en-NL"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Failure Cases

    [Fact]
    public async Task ExecuteAsync_WithDuplicateEmail_ReturnsEmailAlreadyExistsError()
    {
        // Arrange
        var command = new CreateUserCommand("existing@example.com", "Test User", "Password123!", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result<CreateUserResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.User.EmailAlreadyExists");
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateEmail_DoesNotAddUser()
    {
        // Arrange
        var command = new CreateUserCommand("existing@example.com", "Test User", "Password123!", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidEmail_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateUserCommand("invalid-email", "Test User", "Password123!", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateUserResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.EmailInvalidFormat");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyDisplayName_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateUserCommand("test@example.com", "", "Password123!", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateUserResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.User.DisplayNameRequired");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyPassword_ReturnsValidationError()
    {
        // Arrange
        this._passwordHasher.HashPassword("").Returns("");
        var command = new CreateUserCommand("test@example.com", "Test User", "", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateUserResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.Required");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var command = new CreateUserCommand("test@example.com", "Test User", "Password123!", "admin-1");
        this._userRepository.ExistsByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await this._useCase.ExecuteAsync(command, cts.Token);

        // Assert
        await this._userRepository.Received().ExistsByEmailAsync(command.Email, cts.Token);
        await this._userRepository.Received().AddAsync(Arg.Any<User>(), cts.Token);
        await this._userRepository.Received().SaveChangesAsync(cts.Token);
    }

    #endregion
}
