using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
#pragma warning disable CA1826 // Allow LINQ methods on indexable collections for test readability

namespace OpenIdentityStack.Application.Tests.ServiceAccounts;
/// <summary>
/// Unit tests for RotateSecretUseCase.
/// </summary>
public sealed class RotateSecretUseCaseTests
{
    private readonly IServiceAccountRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLog _auditLog;
    private readonly RotateSecretUseCase _useCase;

    public RotateSecretUseCaseTests()
    {
        this._repository = Substitute.For<IServiceAccountRepository>();
        this._passwordHasher = Substitute.For<IPasswordHasher>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._auditLog = Substitute.For<IAuditLog>();

        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        this._passwordHasher.HashPassword(Arg.Any<string>()).Returns(callInfo => $"hashed_{callInfo.Arg<string>()}");

        this._useCase = new RotateSecretUseCase(
            this._repository,
            this._passwordHasher,
            this._dateTimeProvider,
            this._auditLog);
    }

    private ServiceAccount CreateTestServiceAccount()
    {
        Result<ServiceAccount> result = ServiceAccount.Create(
            clientId: "test-service",
            displayName: "Test Service",
            allowedScopes: ["api"],
            allowedGrantTypes: ["client_credentials"],
            this._dateTimeProvider);
        
        return result.Value;
    }

    [Fact]
    public async Task ExecuteAsync_WithValidServiceAccount_RotatesSecret()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new RotateSecretCommand(
            ServiceAccountId: serviceAccount.Id,
            RevokeExisting: false,
            Description: "New rotated secret",
            ExpiresAt: DateTimeOffset.UtcNow.AddDays(90));

        this._repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<RotateSecretResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.NewSecret.ShouldNotBeNullOrEmpty();
        result.Value.CredentialId.ShouldNotBe(Guid.Empty);

        await this._repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNonexistentServiceAccount_ReturnsError()
    {
        // Arrange
        var nonexistentId = ServiceAccountId.Create();
        var command = new RotateSecretCommand(
            ServiceAccountId: nonexistentId,
            RevokeExisting: false,
            Description: null,
            ExpiresAt: null);

        this._repository.GetByIdAsync(nonexistentId, Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        // Act
        Result<RotateSecretResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("ServiceAccount.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledServiceAccount_ReturnsError()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        serviceAccount.Disable(this._dateTimeProvider);

        var command = new RotateSecretCommand(
            ServiceAccountId: serviceAccount.Id,
            RevokeExisting: false,
            Description: null,
            ExpiresAt: null);

        this._repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<RotateSecretResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("ServiceAccount.Disabled");
    }

    [Fact]
    public async Task ExecuteAsync_WithRevokeExisting_RevokesAllPreviousCredentials()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        // Add an existing credential
        serviceAccount.AddCredential("old_hash", "Old credential", null, this._dateTimeProvider);
        Guid existingCredentialId = serviceAccount.Credentials.First().Id;

        var command = new RotateSecretCommand(
            ServiceAccountId: serviceAccount.Id,
            RevokeExisting: true,
            Description: "New secret",
            ExpiresAt: null);

        this._repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<RotateSecretResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        // The old credential should be revoked
        ClientCredential oldCredential = serviceAccount.Credentials.First(c => c.Id == existingCredentialId);
        oldCredential.IsRevoked.ShouldBeTrue();
        
        // A new credential should be added
        serviceAccount.Credentials.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiresAt_SetsCredentialExpiry()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        DateTimeOffset expiryDate = DateTimeOffset.UtcNow.AddDays(30);
        
        var command = new RotateSecretCommand(
            ServiceAccountId: serviceAccount.Id,
            RevokeExisting: false,
            Description: "Expiring secret",
            ExpiresAt: expiryDate);

        this._repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<RotateSecretResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        ClientCredential newCredential = serviceAccount.Credentials.Last();
        newCredential.ExpiresAt.ShouldBe(expiryDate);
    }

    [Fact]
    public async Task ExecuteAsync_LogsAuditEvent()
    {
        // Arrange
        ServiceAccount serviceAccount = this.CreateTestServiceAccount();
        var command = new RotateSecretCommand(
            ServiceAccountId: serviceAccount.Id,
            RevokeExisting: false,
            Description: null,
            ExpiresAt: null);

        this._repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        // Act
        Result<RotateSecretResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await this._auditLog.Received(1).LogAsync(
            Arg.Any<string>(), // userId
            Arg.Is<string>(s => s == "ServiceAccount.SecretRotated"), // action
            Arg.Any<string>(), // entityType
            Arg.Any<string>(), // entityId
            Arg.Any<string?>(), // details
            Arg.Any<CancellationToken>());
    }
}
