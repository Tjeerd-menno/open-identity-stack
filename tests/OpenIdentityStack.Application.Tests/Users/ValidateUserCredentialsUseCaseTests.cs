using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;

/// <summary>
/// Tests for ValidateUserCredentialsUseCase.
/// </summary>
public sealed class ValidateUserCredentialsUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuthenticationSettingsRepository _authSettingsRepository;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IAuditLog _auditLog;
    private readonly ILogger<ValidateUserCredentialsUseCase> _logger;
    private readonly ValidateUserCredentialsUseCase _useCase;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    public ValidateUserCredentialsUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._passwordHasher = Substitute.For<IPasswordHasher>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._authSettingsRepository = Substitute.For<IAuthenticationSettingsRepository>();
        this._permissionChecker = Substitute.For<IPermissionChecker>();
        this._auditLog = Substitute.For<IAuditLog>();
        this._logger = Substitute.For<ILogger<ValidateUserCredentialsUseCase>>();
        this._dateTimeProvider.UtcNow.Returns(this._now);

        // Setup default auth settings - local is default, so local auth is always permitted
        var defaultSettings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._authSettingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(defaultSettings));

        this._useCase = new ValidateUserCredentialsUseCase(
            this._userRepository,
            this._passwordHasher,
            this._dateTimeProvider,
            this._authSettingsRepository,
            this._permissionChecker,
            this._auditLog,
            this._logger);
    }

    #region Success Cases

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        User user = this.CreateActiveUser("test@example.com", "Test User", "hashed_password");
        var command = new ValidateUserCredentialsCommand("test@example.com", "correct_password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        this._passwordHasher.VerifyPassword("hashed_password", "correct_password")
            .Returns(true);

        // Act
        Result<ValidateUserCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(user.Id);
        result.Value.Email.ShouldBe("test@example.com");
        result.Value.DisplayName.ShouldBe("Test User");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_RecordsLogin()
    {
        // Arrange
        User user = this.CreateActiveUser("test@example.com", "Test User", "hashed_password");
        var command = new ValidateUserCredentialsCommand("test@example.com", "correct_password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        this._passwordHasher.VerifyPassword("hashed_password", "correct_password")
            .Returns(true);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        user.LastLoginAt.ShouldBe(this._now);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_SavesChanges()
    {
        // Arrange
        User user = this.CreateActiveUser("test@example.com", "Test User", "hashed_password");
        var command = new ValidateUserCredentialsCommand("test@example.com", "correct_password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        this._passwordHasher.VerifyPassword("hashed_password", "correct_password")
            .Returns(true);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await this._userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region User Not Found

    [Fact]
    public async Task ExecuteAsync_WithNonExistentEmail_ReturnsInvalidCredentials()
    {
        // Arrange
        var command = new ValidateUserCredentialsCommand("notfound@example.com", "password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result<ValidateUserCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Unauthorized.User.InvalidCredentials");
    }

    #endregion

    #region Wrong Password

    [Fact]
    public async Task ExecuteAsync_WithWrongPassword_ReturnsInvalidCredentials()
    {
        // Arrange
        User user = this.CreateActiveUser("test@example.com", "Test User", "hashed_password");
        var command = new ValidateUserCredentialsCommand("test@example.com", "wrong_password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        this._passwordHasher.VerifyPassword("hashed_password", "wrong_password")
            .Returns(false);

        // Act
        Result<ValidateUserCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Unauthorized.User.InvalidCredentials");
    }

    [Fact]
    public async Task ExecuteAsync_WithWrongPassword_DoesNotRecordLogin()
    {
        // Arrange
        User user = this.CreateActiveUser("test@example.com", "Test User", "hashed_password");
        var command = new ValidateUserCredentialsCommand("test@example.com", "wrong_password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        this._passwordHasher.VerifyPassword("hashed_password", "wrong_password")
            .Returns(false);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region User Status Tests

    [Fact]
    public async Task ExecuteAsync_WithDisabledUser_ReturnsAccountDisabled()
    {
        // Arrange
        User user = this.CreateActiveUser("test@example.com", "Test User", "hashed_password");
        user.Disable("Admin action", this._dateTimeProvider);

        var command = new ValidateUserCredentialsCommand("test@example.com", "correct_password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        this._passwordHasher.VerifyPassword("hashed_password", "correct_password")
            .Returns(true);

        // Act
        Result<ValidateUserCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Forbidden.User.AccountDisabled");
    }

    [Fact]
    public async Task ExecuteAsync_WithPendingVerificationUser_ReturnsAccountNotVerified()
    {
        // Arrange
        User user = this.CreatePendingUser("test@example.com", "Test User", "hashed_password");
        var command = new ValidateUserCredentialsCommand("test@example.com", "correct_password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        this._passwordHasher.VerifyPassword("hashed_password", "correct_password")
            .Returns(true);

        // Act
        Result<ValidateUserCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Forbidden.User.AccountNotVerified");
    }

    #endregion

    #region Federated User Tests

    [Fact]
    public async Task ExecuteAsync_WithFederatedOnlyUser_ReturnsInvalidCredentials()
    {
        // Arrange
        User user = User.CreateFederated("federated@example.com", "Federated User", this._dateTimeProvider).Value;
        var command = new ValidateUserCredentialsCommand("federated@example.com", "any_password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        Result<ValidateUserCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Unauthorized.User.InvalidCredentials");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        User user = this.CreateActiveUser("test@example.com", "Test User", "hashed_password");
        var command = new ValidateUserCredentialsCommand("test@example.com", "correct_password");

        this._userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        this._passwordHasher.VerifyPassword("hashed_password", "correct_password")
            .Returns(true);

        // Act
        await this._useCase.ExecuteAsync(command, cts.Token);

        // Assert
        await this._userRepository.Received().GetByEmailAsync(command.Email, cts.Token);
    }

    #endregion

    #region Helper Methods

    private User CreateActiveUser(string email, string displayName, string passwordHash)
    {
        User user = User.CreateLocal(email, displayName, passwordHash, this._dateTimeProvider).Value;
        user.VerifyEmail(this._dateTimeProvider); // Activate the user
        return user;
    }

    private User CreatePendingUser(string email, string displayName, string passwordHash)
    {
        return User.CreateLocal(email, displayName, passwordHash, this._dateTimeProvider).Value;
    }

    #endregion
}
