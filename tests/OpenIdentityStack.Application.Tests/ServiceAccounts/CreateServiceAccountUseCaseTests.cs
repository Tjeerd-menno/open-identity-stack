using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.ServiceAccounts;
/// <summary>
/// Unit tests for CreateServiceAccountUseCase.
/// </summary>
public sealed class CreateServiceAccountUseCaseTests
{
    private readonly IServiceAccountRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLog _auditLog;
    private readonly IClientApplicationRegistrar _registrar;
    private readonly CreateServiceAccountUseCase _useCase;

    public CreateServiceAccountUseCaseTests()
    {
        this._repository = Substitute.For<IServiceAccountRepository>();
        this._passwordHasher = Substitute.For<IPasswordHasher>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._auditLog = Substitute.For<IAuditLog>();
        this._registrar = Substitute.For<IClientApplicationRegistrar>();

        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        this._passwordHasher.HashPassword(Arg.Any<string>()).Returns(callInfo => $"hashed_{callInfo.Arg<string>()}");
        this._registrar
            .RegisterApplicationAccountAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        this._useCase = new CreateServiceAccountUseCase(
            this._repository,
            this._passwordHasher,
            this._dateTimeProvider,
            this._auditLog,
            this._registrar);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_CreatesServiceAccount()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "my-service",
            DisplayName: "My Service Account",
            AllowedScopes: ["api", "openid"],
            AllowedGrantTypes: ["client_credentials"]);

        this._repository.ExistsByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ClientId.ShouldBe("my-service");
        result.Value.DisplayName.ShouldBe("My Service Account");
        result.Value.InitialSecret.ShouldNotBeNullOrEmpty();

        await this._repository.Received(1).AddAsync(Arg.Any<ServiceAccount>(), Arg.Any<CancellationToken>());
        await this._repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateClientId_ReturnsError()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "existing-service",
            DisplayName: "My Service Account",
            AllowedScopes: ["api"],
            AllowedGrantTypes: ["client_credentials"]);

        this._repository.ExistsByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("ServiceAccount.ClientIdExists");

        await this._repository.DidNotReceive().AddAsync(Arg.Any<ServiceAccount>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyClientId_ReturnsError()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "",
            DisplayName: "My Service Account",
            AllowedScopes: ["api"],
            AllowedGrantTypes: ["client_credentials"]);

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("ServiceAccount.ClientIdRequired");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyDisplayName_ReturnsError()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "my-service",
            DisplayName: "",
            AllowedScopes: ["api"],
            AllowedGrantTypes: ["client_credentials"]);

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("ServiceAccount.DisplayNameRequired");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoScopes_ReturnsError()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "my-service",
            DisplayName: "My Service",
            AllowedScopes: [],
            AllowedGrantTypes: ["client_credentials"]);

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("ServiceAccount.ScopesRequired");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidGrantType_ReturnsError()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "my-service",
            DisplayName: "My Service",
            AllowedScopes: ["api"],
            AllowedGrantTypes: ["invalid_grant"]);

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe("ServiceAccount.InvalidGrantType");
    }

    [Fact]
    public async Task ExecuteAsync_AddsInitialCredential()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "my-service",
            DisplayName: "My Service Account",
            AllowedScopes: ["api"],
            AllowedGrantTypes: ["client_credentials"]);

        this._repository.ExistsByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(false);

        ServiceAccount? capturedAccount = null;
        await this._repository.AddAsync(Arg.Do<ServiceAccount>(sa => capturedAccount = sa), Arg.Any<CancellationToken>());

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        capturedAccount.ShouldNotBeNull();
        capturedAccount.Credentials.Count.ShouldBe(1);

        this._passwordHasher.Received(1).HashPassword(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_LogsAuditEvent()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "my-service",
            DisplayName: "My Service Account",
            AllowedScopes: ["api"],
            AllowedGrantTypes: ["client_credentials"]);

        this._repository.ExistsByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await this._auditLog.Received(1).LogAsync(
            Arg.Any<string>(), // userId
            Arg.Is<string>(s => s == "ServiceAccount.Created"), // action
            Arg.Any<string>(), // entityType
            Arg.Any<string>(), // entityId
            Arg.Any<string?>(), // details
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRegistrarFails_ReturnsErrorWithoutPersisting()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "my-service",
            DisplayName: "My Service Account",
            AllowedScopes: ["api"],
            AllowedGrantTypes: ["client_credentials"]);

        this._repository.ExistsByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(false);

        this._registrar
            .RegisterApplicationAccountAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(ServiceAccountErrors.ClientRegistrationFailed);

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ServiceAccount.ClientRegistrationFailed");

        // The service account must not be committed when registration fails.
        await this._repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRegistrarSucceeds_PersistsServiceAccount()
    {
        // Arrange
        var command = new CreateServiceAccountCommand(
            ClientId: "my-service",
            DisplayName: "My Service Account",
            AllowedScopes: ["api"],
            AllowedGrantTypes: ["client_credentials"]);

        this._repository.ExistsByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateServiceAccountResponse> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await this._registrar.Received(1).RegisterApplicationAccountAsync(
            Arg.Is<string>(id => id == "my-service"),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());

        await this._repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
