using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.ServiceAccounts;
/// <summary>
/// Unit tests for the ValidateCertificateUseCase.
/// </summary>
public sealed class ValidateCertificateUseCaseTests
{
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ValidateCertificateUseCase _useCase;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public ValidateCertificateUseCaseTests()
    {
        this._serviceAccountRepository = Substitute.For<IServiceAccountRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);

        this._useCase = new ValidateCertificateUseCase(
            this._serviceAccountRepository,
            this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyClientId_ReturnsClientIdRequiredError()
    {
        // Arrange
        var command = new ValidateCertificateCommand("", "ABC123");

        // Act
        Result<ValidateCertificateResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("ClientId");
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceClientId_ReturnsClientIdRequiredError()
    {
        // Arrange
        var command = new ValidateCertificateCommand("   ", "ABC123");

        // Act
        Result<ValidateCertificateResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("ClientId");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyThumbprint_ReturnsInvalidCredentialsError()
    {
        // Arrange
        var command = new ValidateCertificateCommand("client-id", "");

        // Act
        Result<ValidateCertificateResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("InvalidCredentials");
    }

    [Fact]
    public async Task ExecuteAsync_ServiceAccountNotFound_ReturnsInvalidCredentialsError()
    {
        // Arrange
        var command = new ValidateCertificateCommand("unknown-client", "ABC123");

        this._serviceAccountRepository.GetByClientIdAsync("unknown-client", Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        // Act
        Result<ValidateCertificateResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("InvalidCredentials");
    }

    [Fact]
    public async Task ExecuteAsync_ServiceAccountDisabled_ReturnsAccountDisabledError()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccountWithCertificate("ABC123");
        serviceAccount.Disable(this._dateTimeProvider);
        var command = new ValidateCertificateCommand(serviceAccount.ClientId, "ABC123");

        this._serviceAccountRepository.GetByClientIdAsync(serviceAccount.ClientId, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<ValidateCertificateResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Disabled");
    }

    [Fact]
    public async Task ExecuteAsync_CertificateNotFound_ReturnsInvalidCredentialsError()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccountWithCertificate("ABC123");
        var command = new ValidateCertificateCommand(serviceAccount.ClientId, "DIFFERENT_THUMBPRINT");

        this._serviceAccountRepository.GetByClientIdAsync(serviceAccount.ClientId, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<ValidateCertificateResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("InvalidCredentials");
    }

    [Fact]
    public async Task ExecuteAsync_ValidCertificate_ReturnsSuccess()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccountWithCertificate("ABC123");
        var command = new ValidateCertificateCommand(serviceAccount.ClientId, "ABC123");

        this._serviceAccountRepository.GetByClientIdAsync(serviceAccount.ClientId, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<ValidateCertificateResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ServiceAccountId.ShouldBe(serviceAccount.Id);
        result.Value.ClientId.ShouldBe(serviceAccount.ClientId);
        result.Value.DisplayName.ShouldBe(serviceAccount.DisplayName);
        result.Value.AllowedScopes.ShouldBe(serviceAccount.AllowedScopes);
        result.Value.AllowedGrantTypes.ShouldBe(serviceAccount.AllowedGrantTypes);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccountWithCertificate("ABC123");
        var command = new ValidateCertificateCommand(serviceAccount.ClientId, "ABC123");
        using var cts = new CancellationTokenSource();

        this._serviceAccountRepository.GetByClientIdAsync(serviceAccount.ClientId, cts.Token)
            .Returns(serviceAccount);

        // Act
        await this._useCase.ExecuteAsync(command, cts.Token);

        // Assert
        await this._serviceAccountRepository.Received(1).GetByClientIdAsync(serviceAccount.ClientId, cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredCertificate_ReturnsInvalidCredentialsError()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        // Add an expired certificate
        serviceAccount.AddCertificate("ABC123", "CN=Expired", TestTime.AddDays(-1), this._dateTimeProvider);
        var command = new ValidateCertificateCommand(serviceAccount.ClientId, "ABC123");

        this._serviceAccountRepository.GetByClientIdAsync(serviceAccount.ClientId, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<ValidateCertificateResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("InvalidCredentials");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleValidCertificates_MatchesCorrectOne()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        serviceAccount.AddCertificate("CERT1", "CN=First", TestTime.AddYears(1), this._dateTimeProvider);
        serviceAccount.AddCertificate("CERT2", "CN=Second", TestTime.AddYears(2), this._dateTimeProvider);

        var command = new ValidateCertificateCommand(serviceAccount.ClientId, "CERT2");

        this._serviceAccountRepository.GetByClientIdAsync(serviceAccount.ClientId, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<ValidateCertificateResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    private ServiceAccount CreateTestServiceAccount()
    {
        Result<ServiceAccount> result = ServiceAccount.Create(
            "test-client",
            "Test Service Account",
            ["openid"],
            ["client_credentials"],
            this._dateTimeProvider);

        return result.Value;
    }

    private ServiceAccount CreateTestServiceAccountWithCertificate(string thumbprint)
    {
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        serviceAccount.AddCertificate(
            thumbprint,
            $"CN=Test Certificate {thumbprint}",
            TestTime.AddYears(1),
            this._dateTimeProvider);
        return serviceAccount;
    }
}
