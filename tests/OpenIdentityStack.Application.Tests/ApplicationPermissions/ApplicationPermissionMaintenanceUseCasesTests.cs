using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionMaintenanceUseCasesTests
{
    private static readonly string[] testApplicationResourceWildcard = ["test-application:resource:*"];

    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IApplicationPermissionAuthorizationService authorizationService;
    private readonly IRolePermissionDependencyReader dependencyReader;
    private readonly IPermissionAssignmentStore permissionAssignmentStore;
    private readonly IApplicationPermissionTransactionRunner transactionRunner;
    private readonly IApplicationPermissionAuditWriter auditWriter;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ApplicationPermissionMaintenanceUseCases useCases;

    public ApplicationPermissionMaintenanceUseCasesTests()
    {
        this.repository = Substitute.For<IApplicationPermissionRegistryRepository>();
        this.authorizationService = Substitute.For<IApplicationPermissionAuthorizationService>();
        this.dependencyReader = Substitute.For<IRolePermissionDependencyReader>();
        this.permissionAssignmentStore = Substitute.For<IPermissionAssignmentStore>();
        this.transactionRunner = new PassthroughTransactionRunner();
        this.auditWriter = Substitute.For<IApplicationPermissionAuditWriter>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero));

        this.useCases = new ApplicationPermissionMaintenanceUseCases(
            this.repository,
            this.authorizationService,
            this.dependencyReader,
            this.auditWriter,
            this.dateTimeProvider,
            this.permissionAssignmentStore,
            this.transactionRunner);
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

    [Fact]
    public async Task DeletePermission_WhenActorIsAdmin_TombstonesPermissionAndRemovesAssignments()
    {
        RegisteredApplication application = CreateTestApplication();
        ApplicationPermission permission = application.Permissions[0];
        this.repository.GetByPermissionIdAsync(permission.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.authorizationService.CanAdministerRegistryAsync("admin-1", Arg.Any<CancellationToken>()).Returns(true);
        this.permissionAssignmentStore.RemoveAssignmentsAsync(
                Arg.Any<PermissionAssignmentRemovalPlan>(),
                "admin-1",
                Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<PermissionAssignmentImpactDto>>)new PermissionAssignmentImpactDto[]
            {
                new("role", "role-1", "Test Role", permission.FullPermissionKey, "exactRemoved"),
            });

        Result<DestructiveOperationResultDto> result = await this.useCases.ExecuteAsync(new DeleteApplicationPermissionCommand(
            permission.Id.Value,
            "Permission no longer supported.",
            "admin-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        permission.RemovedAt.ShouldBe(this.dateTimeProvider.UtcNow);
        permission.RemovedBy.ShouldBe("admin-1");
        permission.RemoveReason.ShouldBe("Permission no longer supported.");
        result.Value.RemovedPermissions.Select(item => item.FullPermissionKey).ShouldBe([permission.FullPermissionKey]);
        await this.permissionAssignmentStore.Received(1).RemoveAssignmentsAsync(
            Arg.Is<PermissionAssignmentRemovalPlan>(plan =>
                plan.ExactPermissions.SequenceEqual(new[] { permission.FullPermissionKey })),
            "admin-1",
            Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteApplication_WhenActorIsAdmin_TombstonesApplicationPermissionsAndRemovesNamespaceAssignments()
    {
        RegisteredApplication application = CreateTestApplication();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.authorizationService.CanAdministerRegistryAsync("admin-1", Arg.Any<CancellationToken>()).Returns(true);
        this.permissionAssignmentStore.RemoveAssignmentsAsync(
                Arg.Any<PermissionAssignmentRemovalPlan>(),
                "admin-1",
                Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<PermissionAssignmentImpactDto>>)Array.Empty<PermissionAssignmentImpactDto>());

        Result<DestructiveOperationResultDto> result = await this.useCases.ExecuteAsync(new DeleteRegisteredApplicationCommand(
            application.Id.Value,
            "Application retired.",
            "admin-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        application.IsDeleted.ShouldBeTrue();
        application.DeletedAt.ShouldBe(this.dateTimeProvider.UtcNow);
        application.DeleteReason.ShouldBe("Application retired.");
        application.Permissions.All(permission => permission.IsRemoved).ShouldBeTrue();
        await this.permissionAssignmentStore.Received(1).RemoveAssignmentsAsync(
            Arg.Is<PermissionAssignmentRemovalPlan>(plan =>
                plan.ExactPermissions.SequenceEqual(application.Permissions.Select(permission => permission.FullPermissionKey))
                && plan.WildcardPermissionsToRemove.SequenceEqual(testApplicationResourceWildcard)),
            "admin-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateRegisteredApplication_WhenActorNotAuthorized_ReturnsForbidden()
    {
        // Arrange
        var command = new UpdateRegisteredApplicationCommand(
            Guid.NewGuid(),
            "actor-123",
            "Updated Application",
            "Updated description",
            1);

        RegisteredApplication application = CreateTestApplication();
        this.repository.GetByIdAsync(new RegisteredApplicationId(command.ApplicationId), Arg.Any<CancellationToken>()).Returns(application);
        this.authorizationService.CanManageApplicationAsync("actor-123", application, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        Result<RegisteredApplicationDto> result = await this.useCases.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Forbidden");
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListHistory_WhenActorIsAdmin_ReturnsRemovedPermissionsAndDeletedApplications()
    {
        RegisteredApplication application = CreateTestApplication();
        ApplicationPermission permission = application.Permissions[0];
        application.RemovePermission(permission.Id, "admin-1", "Superseded.", this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        this.repository.ListHistoryAsync("test-application", includeApplications: true, includePermissions: true, Arg.Any<CancellationToken>())
            .Returns(new ApplicationPermissionHistoryDto(
                [ApplicationPermissionMaintenanceUseCases.MapSummary(application)],
                [ApplicationPermissionMaintenanceUseCases.MapRemovedPermission(application, permission)]));
        this.authorizationService.CanAdministerRegistryAsync("admin-1", Arg.Any<CancellationToken>()).Returns(true);

        Result<ApplicationPermissionHistoryDto> result = await this.useCases.ListHistoryAsync(new ListApplicationPermissionHistoryQuery(
            "test-application",
            IncludeApplications: true,
            IncludePermissions: true,
            "admin-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RemovedPermissions.Single().FullPermissionKey.ShouldBe(permission.FullPermissionKey);
        result.Value.DeletedApplications.Single().ApplicationIdentifier.ShouldBe("test-application");
    }

    [Fact]
    public async Task UpdateReplacementGuidance_WhenReplacementIsCurrentPermission_StoresGuidanceAndAudits()
    {
        RegisteredApplication application = CreateTestApplication();
        ApplicationPermission removed = application.Permissions[0];
        application.RemovePermission(removed.Id, "admin-1", "Superseded.", this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        ApplicationPermission replacement = application.AddPermission("resource:list", "List", null, null, "admin-1", this.dateTimeProvider).Value;
        this.repository.GetByPermissionIdAsync(removed.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.repository.GetPermissionByFullKeyAsync(replacement.FullPermissionKey, Arg.Any<CancellationToken>()).Returns(replacement);
        this.authorizationService.CanAdministerRegistryAsync("admin-1", Arg.Any<CancellationToken>()).Returns(true);

        Result<RemovedPermissionDetailDto> result = await this.useCases.UpdateReplacementGuidanceAsync(new UpdateRemovedPermissionReplacementCommand(
            removed.Id.Value,
            replacement.FullPermissionKey,
            "Use list instead.",
            "admin-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        removed.ReplacementFullPermissionKey.ShouldBe(replacement.FullPermissionKey);
        removed.ReplacementNote.ShouldBe("Use list instead.");
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.auditWriter.Received(1).WriteAsync("UpdatePermissionReplacement", "admin-1", application.Id.Value.ToString(), "Succeeded", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateReplacementGuidance_WhenActorIsNotAdmin_ReturnsForbidden()
    {
        RegisteredApplication application = CreateTestApplication();
        ApplicationPermission removed = application.Permissions[0];
        application.RemovePermission(removed.Id, "admin-1", "Superseded.", this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        this.repository.GetByPermissionIdAsync(removed.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.authorizationService.CanAdministerRegistryAsync("writer-1", Arg.Any<CancellationToken>()).Returns(false);

        Result<RemovedPermissionDetailDto> result = await this.useCases.UpdateReplacementGuidanceAsync(new UpdateRemovedPermissionReplacementCommand(
            removed.Id.Value,
            "users:read",
            "Use users read.",
            "writer-1",
            application.ConcurrencyToken));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Forbidden.ApplicationPermission.Forbidden");
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateReplacementGuidance_WhenReplacementDoesNotExist_ReturnsValidationError()
    {
        RegisteredApplication application = CreateTestApplication();
        ApplicationPermission removed = application.Permissions[0];
        application.RemovePermission(removed.Id, "admin-1", "Superseded.", this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        this.repository.GetByPermissionIdAsync(removed.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.repository.GetPermissionByFullKeyAsync("missing-api:resource:read", Arg.Any<CancellationToken>()).Returns((ApplicationPermission?)null);
        this.authorizationService.CanAdministerRegistryAsync("admin-1", Arg.Any<CancellationToken>()).Returns(true);

        Result<RemovedPermissionDetailDto> result = await this.useCases.UpdateReplacementGuidanceAsync(new UpdateRemovedPermissionReplacementCommand(
            removed.Id.Value,
            "missing-api:resource:read",
            "Use this instead.",
            "admin-1",
            application.ConcurrencyToken));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.ApplicationPermission.ReplacementNotFound");
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static RegisteredApplication CreateTestApplication()
    {
        var permissions = new List<(string Key, string DisplayName, string? Description, string? Category)>
        {
            ("resource:read", "Read", "Read permission", null)
        };

        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "test-application",
            "Test Application",
            "Test description",
            "owner-123",
            OwnerType.User,
            permissions,
            "creator-123",
            new TestDateTimeProvider());

        return result.Value;
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset Now => new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
    }
}
