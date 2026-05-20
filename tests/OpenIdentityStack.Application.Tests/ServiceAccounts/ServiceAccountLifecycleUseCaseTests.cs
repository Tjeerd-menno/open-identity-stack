using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Application.Tests.ServiceAccounts;

public sealed class ServiceAccountLifecycleUseCaseTests
{
    private readonly IServiceAccountRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly DisableServiceAccountUseCase disableUseCase;
    private readonly EnableServiceAccountUseCase enableUseCase;
    private readonly DeleteServiceAccountUseCase deleteUseCase;
    private readonly UpdateServiceAccountUseCase updateUseCase;
    private readonly DateTimeOffset now = new(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

    public ServiceAccountLifecycleUseCaseTests()
    {
        this.repository = Substitute.For<IServiceAccountRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.auditLog = Substitute.For<IAuditLog>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.disableUseCase = new DisableServiceAccountUseCase(
            this.repository,
            this.dateTimeProvider,
            this.auditLog);
        this.enableUseCase = new EnableServiceAccountUseCase(
            this.repository,
            this.dateTimeProvider,
            this.auditLog);
        this.deleteUseCase = new DeleteServiceAccountUseCase(this.repository, this.auditLog);
        this.updateUseCase = new UpdateServiceAccountUseCase(
            this.repository,
            this.dateTimeProvider,
            this.auditLog);
    }

    [Fact]
    public async Task DisableExecuteAsync_WhenAccountExists_DisablesAndAudits()
    {
        ServiceAccount serviceAccount = this.CreateServiceAccount();
        this.repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        Result result = await this.disableUseCase.ExecuteAsync(new DisableServiceAccountCommand(serviceAccount.Id));

        result.IsSuccess.ShouldBeTrue();
        serviceAccount.Status.ShouldBe(ServiceAccountStatus.Disabled);
        serviceAccount.ModifiedAt.ShouldBe(this.now);
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "ServiceAccount.Disabled",
            "ServiceAccount",
            serviceAccount.Id.Value.ToString(),
            $"ClientId: {serviceAccount.ClientId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableExecuteAsync_WhenAccountDoesNotExist_ReturnsNotFound()
    {
        var serviceAccountId = ServiceAccountId.Create();
        this.repository.GetByIdAsync(serviceAccountId, Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        Result result = await this.disableUseCase.ExecuteAsync(new DisableServiceAccountCommand(serviceAccountId));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ServiceAccountErrors.NotFound.Code);
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.DidNotReceive().LogAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableExecuteAsync_WhenAccountIsAlreadyDisabled_ReturnsDomainError()
    {
        ServiceAccount serviceAccount = this.CreateServiceAccount();
        serviceAccount.Disable(this.dateTimeProvider);
        this.repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        Result result = await this.disableUseCase.ExecuteAsync(new DisableServiceAccountCommand(serviceAccount.Id));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ServiceAccountErrors.AlreadyDisabled.Code);
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnableExecuteAsync_WhenAccountExists_EnablesAndAudits()
    {
        ServiceAccount serviceAccount = this.CreateServiceAccount();
        serviceAccount.Disable(this.dateTimeProvider);
        this.repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        Result result = await this.enableUseCase.ExecuteAsync(new EnableServiceAccountCommand(serviceAccount.Id));

        result.IsSuccess.ShouldBeTrue();
        serviceAccount.Status.ShouldBe(ServiceAccountStatus.Active);
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "ServiceAccount.Enabled",
            "ServiceAccount",
            serviceAccount.Id.Value.ToString(),
            $"ClientId: {serviceAccount.ClientId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnableExecuteAsync_WhenAccountDoesNotExist_ReturnsNotFound()
    {
        var serviceAccountId = ServiceAccountId.Create();
        this.repository.GetByIdAsync(serviceAccountId, Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        Result result = await this.enableUseCase.ExecuteAsync(new EnableServiceAccountCommand(serviceAccountId));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ServiceAccountErrors.NotFound.Code);
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnableExecuteAsync_WhenAccountIsAlreadyActive_ReturnsDomainError()
    {
        ServiceAccount serviceAccount = this.CreateServiceAccount();
        this.repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        Result result = await this.enableUseCase.ExecuteAsync(new EnableServiceAccountCommand(serviceAccount.Id));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ServiceAccountErrors.AlreadyActive.Code);
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteExecuteAsync_WhenAccountExists_RemovesAndAudits()
    {
        ServiceAccount serviceAccount = this.CreateServiceAccount();
        this.repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        Result result = await this.deleteUseCase.ExecuteAsync(new DeleteServiceAccountCommand(serviceAccount.Id));

        result.IsSuccess.ShouldBeTrue();
        this.repository.Received(1).Remove(serviceAccount);
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "ServiceAccount.Deleted",
            "ServiceAccount",
            serviceAccount.Id.Value.ToString(),
            $"ClientId: {serviceAccount.ClientId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteExecuteAsync_WhenAccountDoesNotExist_ReturnsNotFound()
    {
        var serviceAccountId = ServiceAccountId.Create();
        this.repository.GetByIdAsync(serviceAccountId, Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        Result result = await this.deleteUseCase.ExecuteAsync(new DeleteServiceAccountCommand(serviceAccountId));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ServiceAccountErrors.NotFound.Code);
        this.repository.DidNotReceive().Remove(Arg.Any<ServiceAccount>());
    }

    [Fact]
    public async Task UpdateExecuteAsync_WhenDisplayNameProvided_UpdatesAndAudits()
    {
        ServiceAccount serviceAccount = this.CreateServiceAccount();
        this.repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);
        var command = new UpdateServiceAccountCommand(serviceAccount.Id, " Updated API ");

        Result<UpdateServiceAccountResult> result = await this.updateUseCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ServiceAccountId.ShouldBe(serviceAccount.Id);
        result.Value.UpdatedAt.ShouldBe(this.now);
        serviceAccount.DisplayName.ShouldBe("Updated API");
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "ServiceAccount.Updated",
            "ServiceAccount",
            serviceAccount.Id.Value.ToString(),
            "DisplayName:  Updated API ",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateExecuteAsync_WhenDisplayNameIsBlank_SavesWithoutChangingDisplayName()
    {
        ServiceAccount serviceAccount = this.CreateServiceAccount();
        this.repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        Result<UpdateServiceAccountResult> result = await this.updateUseCase.ExecuteAsync(
            new UpdateServiceAccountCommand(serviceAccount.Id, " "));

        result.IsSuccess.ShouldBeTrue();
        serviceAccount.DisplayName.ShouldBe("Reports API");
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateExecuteAsync_WhenAccountDoesNotExist_ReturnsNotFound()
    {
        var serviceAccountId = ServiceAccountId.Create();
        this.repository.GetByIdAsync(serviceAccountId, Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        Result<UpdateServiceAccountResult> result = await this.updateUseCase.ExecuteAsync(
            new UpdateServiceAccountCommand(serviceAccountId, "Updated"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ServiceAccountErrors.NotFound.Code);
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LifecycleUseCases_PassCancellationToken()
    {
        ServiceAccount serviceAccount = this.CreateServiceAccount();
        using var cts = new CancellationTokenSource();
        this.repository.GetByIdAsync(serviceAccount.Id, cts.Token).Returns(serviceAccount);

        await this.disableUseCase.ExecuteAsync(new DisableServiceAccountCommand(serviceAccount.Id), cts.Token);

        await this.repository.Received(1).GetByIdAsync(serviceAccount.Id, cts.Token);
        await this.repository.Received(1).SaveChangesAsync(cts.Token);
        await this.auditLog.Received(1).LogAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            cts.Token);
    }

    private ServiceAccount CreateServiceAccount()
    {
        return ServiceAccount.Create(
            "reports-api",
            "Reports API",
            ["api.read"],
            ["client_credentials"],
            this.dateTimeProvider).Value;
    }
}
