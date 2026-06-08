using Microsoft.Extensions.DependencyInjection;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionRegistryWorkflowTests
{
    private static readonly DateTimeOffset now = new(2026, 6, 8, 10, 0, 0, TimeSpan.Zero);

    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IApplicationPermissionAuthorizationService authorizationService;
    private readonly IApplicationPermissionAuditWriter auditWriter;
    private readonly IRolePermissionDependencyReader dependencyReader;
    private readonly IPermissionAssignmentStore permissionAssignmentStore;
    private readonly IPermissionDiagnosticsReader diagnosticsReader;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ApplicationPermissionRegistryWorkflow workflow;

    public ApplicationPermissionRegistryWorkflowTests()
    {
        this.repository = Substitute.For<IApplicationPermissionRegistryRepository>();
        this.authorizationService = Substitute.For<IApplicationPermissionAuthorizationService>();
        this.auditWriter = Substitute.For<IApplicationPermissionAuditWriter>();
        this.dependencyReader = Substitute.For<IRolePermissionDependencyReader>();
        this.permissionAssignmentStore = Substitute.For<IPermissionAssignmentStore>();
        this.diagnosticsReader = Substitute.For<IPermissionDiagnosticsReader>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(now);

        this.authorizationService.CanRegisterApplicationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        this.authorizationService.CanManageApplicationAsync(Arg.Any<string>(), Arg.Any<RegisteredApplication>(), Arg.Any<CancellationToken>()).Returns(true);
        this.authorizationService.CanAdministerRegistryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var registerUseCase = new RegisterApplicationUseCase(this.repository, this.authorizationService, this.auditWriter, this.dateTimeProvider);
        var manifestUseCases = new ApplicationPermissionManifestUseCases(
            this.repository,
            this.authorizationService,
            this.auditWriter,
            this.dateTimeProvider,
            this.permissionAssignmentStore,
            new PassthroughTransactionRunner());
        var maintenanceUseCases = new ApplicationPermissionMaintenanceUseCases(
            this.repository,
            this.authorizationService,
            this.dependencyReader,
            this.auditWriter,
            this.dateTimeProvider,
            this.permissionAssignmentStore,
            new PassthroughTransactionRunner(),
            this.diagnosticsReader);

        this.workflow = new ApplicationPermissionRegistryWorkflow(registerUseCase, manifestUseCases, maintenanceUseCases);
    }

    [Fact]
    public async Task RegisterManualApplicationAsync_DelegatesToRegistrationUseCase()
    {
        this.repository.ExistsByIdentifierAsync("orders-api", Arg.Any<CancellationToken>()).Returns(false);

        Result<RegisterApplicationResult> result = await this.workflow.RegisterManualApplicationAsync(new RegisterManualApplicationRequest(
            "orders-api",
            "Orders API",
            "Order permissions",
            "owner-1",
            OwnerType.User,
            "actor-1",
            [new RegisterApplicationPermissionInput("orders:read", "Read orders")]));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationIdentifier.ShouldBe("orders-api");
        await this.repository.Received(1).AddAsync(
            Arg.Is<RegisteredApplication>(application => application.ApplicationIdentifier == "orders-api"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeLifecycleAsync_DelegatesToMaintenanceUseCase()
    {
        RegisteredApplication application = CreateManualApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.dependencyReader.GetDependenciesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);

        Result<RegisteredApplicationDto> result = await this.workflow.ChangeLifecycleAsync(new ChangeLifecycleRequest(
            application.Id.Value,
            ApplicationLifecycleStatus.Disabled,
            "actor-1",
            AcknowledgeDependencies: false,
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(ApplicationLifecycleStatus.Disabled.ToString());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewDeletePermissionAsync_DelegatesToMaintenanceUseCase()
    {
        RegisteredApplication application = CreateManualApplication();
        ApplicationPermission permission = application.Permissions[0];
        this.repository.GetByPermissionIdAsync(permission.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.permissionAssignmentStore.PreviewRemovalImpactAsync(Arg.Any<PermissionAssignmentRemovalPlan>(), Arg.Any<CancellationToken>())
            .Returns([new PermissionAssignmentImpactDto("role", "role-1", "Role 1", permission.FullPermissionKey, "exactRemoved")]);

        Result<DeletionImpactDto> result = await this.workflow.PreviewDeletePermissionAsync(new PreviewDeletePermissionRequest(
            permission.Id.Value,
            "actor-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.TargetId.ShouldBe(permission.Id.Value);
        await this.permissionAssignmentStore.Received(1).PreviewRemovalImpactAsync(
            Arg.Is<PermissionAssignmentRemovalPlan>(plan => plan.ExactPermissions.SequenceEqual(new[] { permission.FullPermissionKey })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePermissionAsync_DelegatesToMaintenanceUseCase()
    {
        RegisteredApplication application = CreateManualApplication();
        ApplicationPermission permission = application.Permissions[0];
        this.repository.GetByPermissionIdAsync(permission.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.permissionAssignmentStore.RemoveAssignmentsAsync(
                Arg.Any<PermissionAssignmentRemovalPlan>(),
                "actor-1",
                Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<PermissionAssignmentImpactDto>>)Array.Empty<PermissionAssignmentImpactDto>());

        Result<DestructiveOperationResultDto> result = await this.workflow.DeletePermissionAsync(new DeletePermissionRequest(
            permission.Id.Value,
            "Retired.",
            "actor-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        permission.IsRemoved.ShouldBeTrue();
        await this.permissionAssignmentStore.Received(1).RemoveAssignmentsAsync(
            Arg.Any<PermissionAssignmentRemovalPlan>(),
            "actor-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewManifestAsync_DelegatesToManifestUseCase()
    {
        RegisteredApplication application = CreateManifestBackedApplication("1.0.0");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<ManifestPreviewDto> result = await this.workflow.PreviewManifestAsync(new PreviewManifestRequest(
            application.Id.Value,
            CreateManifest("1.1.0"),
            "actor-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequestedManifestVersion.ShouldBe("1.1.0");
        result.Value.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public async Task ListDiagnosticsAsync_DelegatesToMaintenanceUseCase()
    {
        var diagnostic = new PermissionDiagnosticIssueDto("missing", "role", "role-1", "Role 1", "orders-api:orders:read", "Missing registry entry.");
        this.diagnosticsReader.ListIssuesAsync(Arg.Any<CancellationToken>()).Returns(new PermissionDiagnosticsDto([diagnostic]));

        Result<PermissionDiagnosticsDto> result = await this.workflow.ListDiagnosticsAsync(new ListDiagnosticsRequest("actor-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Issues.ShouldBe([diagnostic]);
        await this.diagnosticsReader.Received(1).ListIssuesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AddApplication_RegistersWorkflowDependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton(this.repository);
        services.AddSingleton(this.authorizationService);
        services.AddSingleton(this.auditWriter);
        services.AddSingleton(this.dependencyReader);
        services.AddSingleton(this.permissionAssignmentStore);
        services.AddSingleton<IApplicationPermissionTransactionRunner, PassthroughTransactionRunner>();
        services.AddSingleton(this.diagnosticsReader);
        services.AddSingleton(this.dateTimeProvider);

        services.AddApplication();

        ServiceDescriptor descriptor = services.Single(service => service.ServiceType == typeof(IApplicationPermissionRegistryWorkflow));
        descriptor.ImplementationType.ShouldBe(typeof(ApplicationPermissionRegistryWorkflow));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    private static RegisteredApplication CreateManualApplication()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            "Order permissions",
            "owner-1",
            OwnerType.User,
            [("orders:read", "Read orders", (string?)null, "Orders")],
            "actor-1",
            new StaticDateTimeProvider(now));

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static RegisteredApplication CreateManifestBackedApplication(string version)
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            "Order permissions",
            "owner-1",
            OwnerType.User,
            [("orders:read", "Read orders", (string?)null, "Orders")],
            "actor-1",
            new StaticDateTimeProvider(now),
            schemaVersion: "1.0.0",
            manifestVersion: version,
            manifestBaseUrl: "https://orders.example.com");

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static PermissionManifestDocument CreateManifest(string version)
    {
        return new PermissionManifestDocument(
            "1.0.0",
            new PermissionManifestApplicationDeclaration("orders-api", "Orders API", "Order permissions", version),
            [
                new PermissionManifestPermissionDeclaration("orders:read", "Read orders", null, "Orders"),
                new PermissionManifestPermissionDeclaration("orders:write", "Write orders", null, "Orders"),
            ]);
    }

    private sealed class PassthroughTransactionRunner : IApplicationPermissionTransactionRunner
    {
        public Task<Result<T>> ExecuteAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken = default)
        {
            return operation(cancellationToken);
        }
    }

    private sealed class StaticDateTimeProvider : IDateTimeProvider
    {
        public StaticDateTimeProvider(DateTimeOffset utcNow)
        {
            this.UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }

        public DateTimeOffset Now => this.UtcNow;
    }
}
