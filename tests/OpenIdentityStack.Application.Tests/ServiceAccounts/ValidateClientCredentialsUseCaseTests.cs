using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.ServiceAccounts;

/// <summary>
/// Tests for ValidateClientCredentialsUseCase.
/// </summary>
public sealed class ValidateClientCredentialsUseCaseTests
{
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ValidateClientCredentialsUseCase _useCase;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    public ValidateClientCredentialsUseCaseTests()
    {
        this._serviceAccountRepository = Substitute.For<IServiceAccountRepository>();
        this._passwordHasher = Substitute.For<IPasswordHasher>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);

        this._useCase = new ValidateClientCredentialsUseCase(
            this._serviceAccountRepository,
            this._passwordHasher,
            this._dateTimeProvider);
    }

    private ServiceAccount CreateTestServiceAccount(
        string clientId = "test-client",
        ServiceAccountStatus status = ServiceAccountStatus.Active)
    {
        Result<ServiceAccount> result = ServiceAccount.Create(
            clientId,
            "Test Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);

        ServiceAccount account = result.Value;
        
        // Add a credential
        account.AddCredential("hashed_secret", "Test Key", null, this._dateTimeProvider);

        if (status == ServiceAccountStatus.Disabled)
        {
            account.Disable(this._dateTimeProvider);
        }

        return account;
    }

    #region Success Cases

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new ValidateClientCredentialsCommand("test-client", "valid_secret");

        this._serviceAccountRepository.GetByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(serviceAccount);
        this._passwordHasher.VerifyPassword("hashed_secret", "valid_secret")
            .Returns(true);

        // Act
        Result<ValidateClientCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientId.ShouldBe("test-client");
        result.Value.ServiceAccountId.ShouldBe(serviceAccount.Id);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_ReturnsAllowedScopes()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new ValidateClientCredentialsCommand("test-client", "valid_secret");

        this._serviceAccountRepository.GetByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(serviceAccount);
        this._passwordHasher.VerifyPassword(Arg.Any<string>(), "valid_secret")
            .Returns(true);

        // Act
        Result<ValidateClientCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.AllowedScopes.ShouldContain("api:read");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_RecordsCredentialUsage()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new ValidateClientCredentialsCommand("test-client", "valid_secret");

        this._serviceAccountRepository.GetByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(serviceAccount);
        this._passwordHasher.VerifyPassword(Arg.Any<string>(), "valid_secret")
            .Returns(true);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._serviceAccountRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Invalid Input Cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptyClientId_ReturnsFailure(string? clientId)
    {
        // Arrange
        var command = new ValidateClientCredentialsCommand(clientId!, "some_secret");

        // Act
        Result<ValidateClientCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.ClientIdRequired");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithEmptySecret_ReturnsFailure(string? secret)
    {
        // Arrange
        var command = new ValidateClientCredentialsCommand("test-client", secret!);

        // Act
        Result<ValidateClientCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.InvalidCredentials");
    }

    #endregion

    #region Service Account Not Found

    [Fact]
    public async Task ExecuteAsync_WithNonExistentClientId_ReturnsFailure()
    {
        // Arrange
        var command = new ValidateClientCredentialsCommand("unknown-client", "some_secret");

        this._serviceAccountRepository.GetByClientIdAsync("unknown-client", Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        // Act
        Result<ValidateClientCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.InvalidCredentials");
    }

    #endregion

    #region Disabled Account

    [Fact]
    public async Task ExecuteAsync_WithDisabledAccount_ReturnsFailure()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount(status: ServiceAccountStatus.Disabled);
        var command = new ValidateClientCredentialsCommand("test-client", "valid_secret");

        this._serviceAccountRepository.GetByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<ValidateClientCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.AccountDisabled");
    }

    #endregion

    #region Invalid Credentials

    [Fact]
    public async Task ExecuteAsync_WithWrongSecret_ReturnsFailure()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new ValidateClientCredentialsCommand("test-client", "wrong_secret");

        this._serviceAccountRepository.GetByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(serviceAccount);
        this._passwordHasher.VerifyPassword(Arg.Any<string>(), "wrong_secret")
            .Returns(false);

        // Act
        Result<ValidateClientCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.InvalidCredentials");
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredCredential_ReturnsFailure()
    {
        // Arrange
        Result<ServiceAccount> accountResult = ServiceAccount.Create(
            "test-client",
            "Test Service",
            ["api:read"],
            ["client_credentials"],
            this._dateTimeProvider);
        ServiceAccount account = accountResult.Value;
        
        // Add an expired credential
        account.AddCredential("hashed_secret", "Expired Key", this._now.AddDays(-1), this._dateTimeProvider);

        var command = new ValidateClientCredentialsCommand("test-client", "valid_secret");

        this._serviceAccountRepository.GetByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(account);
        this._passwordHasher.VerifyPassword(Arg.Any<string>(), "valid_secret")
            .Returns(true);

        // Act
        Result<ValidateClientCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.InvalidCredentials");
    }

    [Fact]
    public async Task ExecuteAsync_WithRevokedCredential_ReturnsFailure()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        Guid credentialId = serviceAccount.Credentials[0].Id;
        serviceAccount.RevokeCredential(credentialId, this._dateTimeProvider);

        var command = new ValidateClientCredentialsCommand("test-client", "valid_secret");

        this._serviceAccountRepository.GetByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns(serviceAccount);
        this._passwordHasher.VerifyPassword(Arg.Any<string>(), "valid_secret")
            .Returns(true);

        // Act
        Result<ValidateClientCredentialsResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.InvalidCredentials");
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task ExecuteAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var command = new ValidateClientCredentialsCommand("test-client", "secret");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        this._serviceAccountRepository.GetByClientIdAsync("test-client", Arg.Any<CancellationToken>())
            .Returns<ServiceAccount?>(_ => throw new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => this._useCase.ExecuteAsync(command, cts.Token));
    }

    #endregion
}
