using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.ServiceAccounts;
/// <summary>
/// Unit tests for the AddCertificateUseCase.
/// </summary>
public sealed class AddCertificateUseCaseTests
{
    private readonly IServiceAccountRepository _serviceAccountRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLog _auditLog;
    private readonly AddCertificateUseCase _useCase;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public AddCertificateUseCaseTests()
    {
        this._serviceAccountRepository = Substitute.For<IServiceAccountRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._auditLog = Substitute.For<IAuditLog>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);

        this._useCase = new AddCertificateUseCase(
            this._serviceAccountRepository,
            this._dateTimeProvider,
            this._auditLog);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceAccountNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var serviceAccountId = new ServiceAccountId(Guid.NewGuid());
        var command = new AddCertificateCommand(
            serviceAccountId,
            "ABC123",
            "CN=Test",
            TestTime.AddYears(1));

        this._serviceAccountRepository.GetByIdAsync(serviceAccountId, Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        // Act
        Result<AddCertificateResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_ServiceAccountDisabled_ReturnsDisabledError()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        serviceAccount.Disable(this._dateTimeProvider);
        var command = new AddCertificateCommand(
            serviceAccount.Id,
            "ABC123",
            "CN=Test",
            TestTime.AddYears(1));

        this._serviceAccountRepository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<AddCertificateResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Disabled");
    }

    [Fact]
    public async Task ExecuteAsync_ValidCertificate_AddsCertificateAndReturnsSuccess()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new AddCertificateCommand(
            serviceAccount.Id,
            "ABC123",
            "CN=Test Certificate",
            TestTime.AddYears(1));

        this._serviceAccountRepository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<AddCertificateResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CertificateId.ShouldNotBe(Guid.Empty);
        serviceAccount.Certificates.ShouldContain(c => c.Thumbprint == "ABC123");
        await this._serviceAccountRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ValidCertificate_LogsAuditEvent()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new AddCertificateCommand(
            serviceAccount.Id,
            "ABC123",
            "CN=Test Certificate",
            TestTime.AddYears(1));

        this._serviceAccountRepository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._auditLog.Received(1).LogAsync(
            Arg.Is<string>(s => s == "system"),
            Arg.Is<string>(s => s == "ServiceAccount.CertificateAdded"),
            Arg.Is<string>(s => s == "ServiceAccount"),
            Arg.Is<string>(s => s == serviceAccount.Id.Value.ToString()),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateThumbprint_ReturnsError()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        serviceAccount.AddCertificate("ABC123", "CN=First", TestTime.AddYears(1), this._dateTimeProvider);

        var command = new AddCertificateCommand(
            serviceAccount.Id,
            "ABC123", // Duplicate thumbprint
            "CN=Second",
            TestTime.AddYears(1));

        this._serviceAccountRepository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<AddCertificateResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new AddCertificateCommand(
            serviceAccount.Id,
            "ABC123",
            "CN=Test",
            TestTime.AddYears(1));

        using var cts = new CancellationTokenSource();

        this._serviceAccountRepository.GetByIdAsync(serviceAccount.Id, cts.Token)
            .Returns(serviceAccount);

        // Act
        await this._useCase.ExecuteAsync(command, cts.Token);

        // Assert
        await this._serviceAccountRepository.Received(1).GetByIdAsync(serviceAccount.Id, cts.Token);
        await this._serviceAccountRepository.Received(1).SaveChangesAsync(cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredCertificate_StillAddsCertificate()
    {
        // Note: The domain allows adding certificates regardless of expiration date.
        // Expiration is checked at validation time, not at add time.
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new AddCertificateCommand(
            serviceAccount.Id,
            "ABC123",
            "CN=Test",
            TestTime.AddDays(-1)); // Already expired

        this._serviceAccountRepository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<AddCertificateResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert - Certificate is added but will fail validation later
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
}
