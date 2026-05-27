using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class ApplicationLifecycleUseCaseTests
{
    private readonly IApplicationRepository repository;
    private readonly IApplicationProtocolProjection projection;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly ApplicationLifecycleUseCases useCases;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public ApplicationLifecycleUseCaseTests()
    {
        this.repository = Substitute.For<IApplicationRepository>();
        this.projection = Substitute.For<IApplicationProtocolProjection>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.auditLog = Substitute.For<IAuditLog>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        this.projection.DeleteAsync(Arg.Any<DomainApplicationId>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        this.useCases = new ApplicationLifecycleUseCases(
            this.repository,
            this.projection,
            this.dateTimeProvider,
            this.auditLog);
    }

    [Fact]
    public async Task CreateExecuteAsync_WhenClientIdIsUnique_CreatesApplicationProjectsAndAudits()
    {
        var command = new CreateApplicationCommand(
            "orders-web",
            "Orders Web",
            "Orders portal",
            ApplicationProfile.Web,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["openid", "orders.read"],
            ["https://orders.example.com/callback"],
            [],
            RequirePkce: false,
            RequireConsent: true);

        Result<ApplicationCommandResult> result = await this.useCases.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientId.ShouldBe("orders-web");
        result.Value.DisplayName.ShouldBe("Orders Web");
        result.Value.Status.ShouldBe(ApplicationStatus.Active);
        await this.repository.Received(1).AddAsync(Arg.Is<DomainApplication>(a => a.ClientId == "orders-web"), Arg.Any<CancellationToken>());
        await this.projection.Received(1).UpsertAsync(Arg.Is<DomainApplication>(a => a.ClientId == "orders-web"), Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "Application.Created",
            "Application",
            result.Value.Id.Value.ToString(),
            "ClientId: orders-web, DisplayName: Orders Web",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateExecuteAsync_WhenClientIdExists_ReturnsConflictWithoutSaving()
    {
        this.repository.ExistsByClientIdAsync("orders-web", Arg.Any<CancellationToken>())
            .Returns(true);

        Result<ApplicationCommandResult> result = await this.useCases.ExecuteAsync(new CreateApplicationCommand(
            "orders-web",
            "Orders Web",
            null,
            ApplicationProfile.Web,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["openid"],
            ["https://orders.example.com/callback"],
            [],
            RequirePkce: false,
            RequireConsent: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.ClientIdExists.Code);
        await this.repository.DidNotReceive().AddAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>());
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMetadataExecuteAsync_WhenApplicationExists_UpdatesProjectsSavesAndAudits()
    {
        DomainApplication application = this.CreateApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result<ApplicationCommandResult> result = await this.useCases.ExecuteAsync(
            new UpdateApplicationMetadataCommand(application.Id, " Updated ", " Description "));

        result.IsSuccess.ShouldBeTrue();
        application.DisplayName.ShouldBe("Updated");
        application.Description.ShouldBe("Description");
        await this.projection.Received(1).UpsertAsync(application, Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "Application.Updated",
            "Application",
            application.Id.Value.ToString(),
            "ClientId: orders-web",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableExecuteAsync_WhenApplicationExists_DisablesProjectsSavesAndAudits()
    {
        DomainApplication application = this.CreateApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result result = await this.useCases.ExecuteAsync(new DisableApplicationCommand(application.Id));

        result.IsSuccess.ShouldBeTrue();
        application.Status.ShouldBe(ApplicationStatus.Disabled);
        await this.projection.Received(1).UpsertAsync(application, Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "Application.Disabled",
            "Application",
            application.Id.Value.ToString(),
            "ClientId: orders-web",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteExecuteAsync_WhenApplicationExists_RemovesDeletesProjectionSavesAndAudits()
    {
        DomainApplication application = this.CreateApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result result = await this.useCases.ExecuteAsync(new DeleteApplicationCommand(application.Id));

        result.IsSuccess.ShouldBeTrue();
        this.repository.Received(1).Remove(application);
        await this.projection.Received(1).DeleteAsync(application.Id, Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditLog.Received(1).LogAsync(
            "system",
            "Application.Deleted",
            "Application",
            application.Id.Value.ToString(),
            "ClientId: orders-web",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LifecycleUseCases_PassCancellationToken()
    {
        DomainApplication application = this.CreateApplication();
        using var cts = new CancellationTokenSource();
        this.repository.GetByIdAsync(application.Id, cts.Token).Returns(application);

        await this.useCases.ExecuteAsync(new DisableApplicationCommand(application.Id), cts.Token);

        await this.repository.Received(1).GetByIdAsync(application.Id, cts.Token);
        await this.projection.Received(1).UpsertAsync(application, cts.Token);
        await this.repository.Received(1).SaveChangesAsync(cts.Token);
    }

    private DomainApplication CreateApplication() =>
        DomainApplication.Create(
            "orders-web",
            "Orders Web",
            null,
            ApplicationProfile.Web,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["openid"],
            ["https://orders.example.com/callback"],
            [],
            requirePkce: false,
            requireConsent: true,
            this.dateTimeProvider).Value;
}
