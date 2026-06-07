using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.ApplicationPermissions;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionManifestUseCaseTests
{
    private static readonly string[] cancelExactPermission = ["orders-api:order:cancel"];
    private static readonly string[] readExactPermission = ["orders-api:order:read"];
    private static readonly string[] orderWildcardPermission = ["orders-api:order:*"];

    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IApplicationPermissionAuthorizationService authorizationService;
    private readonly IApplicationPermissionAuditWriter auditWriter;
    private readonly IPermissionAssignmentStore permissionAssignmentStore;
    private readonly IApplicationPermissionTransactionRunner transactionRunner;
    private readonly IRemotePermissionManifestFetcher remoteManifestFetcher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ApplicationPermissionManifestUseCases useCases;

    public ApplicationPermissionManifestUseCaseTests()
    {
        this.repository = Substitute.For<IApplicationPermissionRegistryRepository>();
        this.authorizationService = Substitute.For<IApplicationPermissionAuthorizationService>();
        this.auditWriter = Substitute.For<IApplicationPermissionAuditWriter>();
        this.permissionAssignmentStore = Substitute.For<IPermissionAssignmentStore>();
        this.remoteManifestFetcher = Substitute.For<IRemotePermissionManifestFetcher>();
        this.transactionRunner = new PassthroughTransactionRunner();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 5, 29, 10, 0, 0, TimeSpan.Zero));
        this.authorizationService.CanRegisterApplicationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        this.authorizationService.CanManageApplicationAsync(Arg.Any<string>(), Arg.Any<RegisteredApplication>(), Arg.Any<CancellationToken>()).Returns(true);

        this.useCases = new ApplicationPermissionManifestUseCases(
            this.repository,
            this.authorizationService,
            this.auditWriter,
            this.dateTimeProvider,
            this.permissionAssignmentStore,
            this.transactionRunner,
            remoteManifestFetcher: this.remoteManifestFetcher);
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
    public async Task CreateAsync_WithValidManifest_StoresCurrentApplicationAndManifestMetadata()
    {
        this.repository.ExistsByIdentifierAsync("orders-api", Arg.Any<CancellationToken>()).Returns(false);

        Result<RegisteredApplicationDto> result = await this.useCases.CreateAsync(new CreateApplicationPermissionManifestCommand(
            ValidManifest("1.0.0"),
            "owner-1",
            OwnerType.User,
            "https://orders.example.com/api",
            "actor-1",
            IsImported: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationIdentifier.ShouldBe("orders-api");
        result.Value.ManifestVersion.ShouldBe("1.0.0");
        result.Value.SchemaVersion.ShouldBe("1.0.0");
        result.Value.ManifestBaseUrl.ShouldBe("https://orders.example.com/api");
        result.Value.Permissions[0].PermissionKey.ShouldBe("order:read");
        result.Value.Permissions[0].FullPermissionKey.ShouldBe("orders-api:order:read");
        await this.repository.Received(1).AddAsync(
            Arg.Is<RegisteredApplication>(application =>
                application.ApplicationIdentifier == "orders-api"
                && application.ManifestVersion == "1.0.0"
                && application.ManifestBaseUrl == "https://orders.example.com/api"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateManifestBaseUrl_ReturnsConflict()
    {
        this.repository.ExistsByIdentifierAsync("orders-api", Arg.Any<CancellationToken>()).Returns(false);
        this.repository.ExistsByManifestBaseUrlAsync("https://orders.example.com/api", Arg.Any<CancellationToken>()).Returns(true);

        Result<RegisteredApplicationDto> result = await this.useCases.CreateAsync(new CreateApplicationPermissionManifestCommand(
            ValidManifest("1.0.0"),
            "owner-1",
            OwnerType.User,
            "https://orders.example.com/api",
            "actor-1",
            IsImported: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.PermissionManifest.ManifestBaseUrlConflict");
        await this.repository.DidNotReceive().AddAsync(Arg.Any<RegisteredApplication>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithManualRegistrationAndManifestBaseUrl_ReturnsValidationError()
    {
        Result<RegisteredApplicationDto> result = await this.useCases.CreateAsync(new CreateApplicationPermissionManifestCommand(
            ValidManifest("1.0.0"),
            "owner-1",
            OwnerType.User,
            "https://orders.example.com/api",
            "actor-1"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.ManualApplicationCannotBeManifestBacked");
        await this.repository.DidNotReceive().AddAsync(Arg.Any<RegisteredApplication>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDetailAsync_AfterCreate_ReturnsManifestMetadata()
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<RegisteredApplicationDto> result = await this.useCases.GetDetailAsync(new GetRegisteredApplicationQuery(application.Id.Value));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ManifestVersion.ShouldBe("1.0.0");
        result.Value.Permissions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ApplyAsync_WithNewerNonDestructiveManifest_AddsPermissionsAndAdvancesVersion()
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<RegisteredApplicationDto> result = await this.useCases.ApplyAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            ValidManifest("1.1.0") with
            {
                Permissions =
                [
                    new PermissionManifestPermissionDeclaration("order:read", "Read order", null, "Orders"),
                    new PermissionManifestPermissionDeclaration("order:cancel", "Cancel order", null, "Orders"),
                ],
            },
            "actor-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ManifestVersion.ShouldBe("1.1.0");
        result.Value.Permissions.Select(permission => permission.PermissionKey).ShouldBe(["order:read", "order:cancel"]);
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.9.9")]
    public async Task ApplyAsync_WithSameOrOlderManifestVersion_ReturnsConflict(string version)
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<RegisteredApplicationDto> result = await this.useCases.ApplyAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            ValidManifest(version),
            "actor-1",
            application.ConcurrencyToken));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.PermissionManifest.VersionNotNewer");
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAsync_WithNumericallyGreaterPrereleaseVersion_IsAccepted()
    {
        RegisteredApplication application = CreateApplication("1.0.0-alpha.2");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<RegisteredApplicationDto> result = await this.useCases.ApplyAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            ValidManifest("1.0.0-alpha.10"),
            "actor-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ManifestVersion.ShouldBe("1.0.0-alpha.10");
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewChangesAsync_WithOmittedExistingPermission_ReturnsDestructiveImpactWithoutSaving()
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        application.AddPermission("order:cancel", "Cancel order", null, "Orders", "actor-1", this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.permissionAssignmentStore.PreviewRemovalImpactAsync(
                Arg.Any<PermissionAssignmentRemovalPlan>(),
                Arg.Any<CancellationToken>())
            .Returns([
                new PermissionAssignmentImpactDto("role", "role-1", "Order Admin", "orders-api:order:cancel", "exactRemoved"),
                new PermissionAssignmentImpactDto("role", "role-2", "Order Aggregate", "orders-api:order:*", "wildcardImpacted"),
            ]);

        Result<ManifestPreviewDto> result = await this.useCases.PreviewChangesAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            ValidManifest("1.1.0"),
            "actor-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsDestructive.ShouldBeTrue();
        result.Value.Removals.Select(permission => permission.FullPermissionKey).ShouldBe(["orders-api:order:cancel"]);
        result.Value.AssignmentImpacts.Select(impact => impact.ImpactKind).ShouldBe(["exactRemoved", "wildcardImpacted"]);
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAsync_WithOmittedExistingPermission_TombstonesPermissionRemovesAssignmentsAndAdvancesVersion()
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        ApplicationPermission cancelPermission = application.AddPermission("order:cancel", "Cancel order", null, "Orders", "actor-1", this.dateTimeProvider).Value;
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.permissionAssignmentStore.RemoveAssignmentsAsync(
                Arg.Any<PermissionAssignmentRemovalPlan>(),
                "actor-1",
                Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<PermissionAssignmentImpactDto>>)new PermissionAssignmentImpactDto[]
            {
                new PermissionAssignmentImpactDto("role", "role-1", "Order Admin", "orders-api:order:cancel", "exactRemoved"),
            });

        Result<ManifestApplyDto> result = await this.useCases.ApplyChangesAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            ValidManifest("1.1.0"),
            "actor-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Application.ManifestVersion.ShouldBe("1.1.0");
        result.Value.Result.RemovedPermissions.Select(permission => permission.FullPermissionKey).ShouldBe(["orders-api:order:cancel"]);
        cancelPermission.RemovedAt.ShouldBe(this.dateTimeProvider.UtcNow);
        cancelPermission.RemovedBy.ShouldBe("actor-1");
        cancelPermission.RemoveReason.ShouldBe("Manifest 1.1.0 omitted permission.");
        await this.permissionAssignmentStore.Received(1).RemoveAssignmentsAsync(
            Arg.Is<PermissionAssignmentRemovalPlan>(plan =>
                plan.ExactPermissions.SequenceEqual(cancelExactPermission)
                && plan.WildcardPermissionsToRemove.Count == 0),
            "actor-1",
            Arg.Any<CancellationToken>());
        await this.repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAsync_WhenOmissionRemovesLastAggregatePermission_RemovesCollapsedWildcardAssignments()
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.permissionAssignmentStore.RemoveAssignmentsAsync(
                Arg.Any<PermissionAssignmentRemovalPlan>(),
                "actor-1",
                Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<PermissionAssignmentImpactDto>>)new PermissionAssignmentImpactDto[]
            {
                new PermissionAssignmentImpactDto("role", "role-1", "Order Admin", "orders-api:order:read", "exactRemoved"),
                new PermissionAssignmentImpactDto("role", "role-2", "Order Aggregate", "orders-api:order:*", "wildcardRemoved"),
            });

        Result<ManifestApplyDto> result = await this.useCases.ApplyChangesAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            EmptyManifest("1.1.0"),
            "actor-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Result.WildcardAssignmentsRemoved.Select(impact => impact.Permission).ShouldBe(["orders-api:order:*"]);
        await this.permissionAssignmentStore.Received(1).RemoveAssignmentsAsync(
            Arg.Is<PermissionAssignmentRemovalPlan>(plan =>
                plan.ExactPermissions.SequenceEqual(readExactPermission)
                && plan.WildcardPermissionsToRemove.SequenceEqual(orderWildcardPermission)),
            "actor-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewChangesAsync_WhenAdditionExpandsExistingWildcardWithoutAcknowledgement_ReturnsConflict()
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.permissionAssignmentStore.PreviewRemovalImpactAsync(
                Arg.Any<PermissionAssignmentRemovalPlan>(),
                Arg.Any<CancellationToken>())
            .Returns([
                new PermissionAssignmentImpactDto("role", "role-1", "Order Aggregate", "orders-api:order:*", "wildcardImpacted"),
            ]);

        Result<ManifestPreviewDto> result = await this.useCases.PreviewChangesAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            ValidManifest("1.1.0") with
            {
                Permissions =
                [
                    new PermissionManifestPermissionDeclaration("order:read", "Read order", null, "Orders"),
                    new PermissionManifestPermissionDeclaration("order:cancel", "Cancel order", null, "Orders"),
                ],
            },
            "actor-1",
            application.ConcurrencyToken));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.PermissionManifest.WildcardImpactAcknowledgementRequired");
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewChangesAsync_WhenAdditionExpandsExistingWildcardWithAcknowledgement_ReturnsImpact()
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.permissionAssignmentStore.PreviewRemovalImpactAsync(
                Arg.Any<PermissionAssignmentRemovalPlan>(),
                Arg.Any<CancellationToken>())
            .Returns([
                new PermissionAssignmentImpactDto("role", "role-1", "Order Aggregate", "orders-api:order:*", "wildcardImpacted"),
            ]);

        Result<ManifestPreviewDto> result = await this.useCases.PreviewChangesAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            ValidManifest("1.1.0") with
            {
                Permissions =
                [
                    new PermissionManifestPermissionDeclaration("order:read", "Read order", null, "Orders"),
                    new PermissionManifestPermissionDeclaration("order:cancel", "Cancel order", null, "Orders"),
                ],
            },
            "actor-1",
            application.ConcurrencyToken,
            AcknowledgeWildcardImpact: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsDestructive.ShouldBeFalse();
        result.Value.Additions.Select(permission => permission.FullPermissionKey).ShouldBe(["orders-api:order:cancel"]);
        result.Value.AssignmentImpacts.Select(impact => impact.ImpactKind).ShouldBe(["wildcardImpacted"]);
        await this.permissionAssignmentStore.Received(1).PreviewRemovalImpactAsync(
            Arg.Is<PermissionAssignmentRemovalPlan>(plan =>
                plan.ExactPermissions.SequenceEqual(cancelExactPermission)
                && plan.WildcardPermissionsToRemove.Count == 0),
            Arg.Any<CancellationToken>());
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyAsync_WhenAssignmentRemovalFails_DoesNotTombstoneOrSave()
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        ApplicationPermission cancelPermission = application.AddPermission("order:cancel", "Cancel order", null, "Orders", "actor-1", this.dateTimeProvider).Value;
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.permissionAssignmentStore.RemoveAssignmentsAsync(
                Arg.Any<PermissionAssignmentRemovalPlan>(),
                "actor-1",
                Arg.Any<CancellationToken>())
            .Returns(DomainError.Conflict("PermissionAssignmentStore.Failed", "Assignment removal failed."));

        Result<ManifestApplyDto> result = await this.useCases.ApplyChangesAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            ValidManifest("1.1.0"),
            "actor-1",
            application.ConcurrencyToken));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.PermissionAssignmentStore.Failed");
        cancelPermission.RemovedAt.ShouldBeNull();
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreviewRemoteChangesAsync_FetchesTrustedManifestAndReturnsInlinePreview()
    {
        RegisteredApplication application = CreateApplication("1.0.0");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        this.remoteManifestFetcher.FetchAsync("https://orders.example.com/api", "orders-api", Arg.Any<CancellationToken>())
            .Returns(ValidManifest("1.1.0"));

        Result<ManifestPreviewDto> result = await this.useCases.PreviewRemoteChangesAsync(new RemoteApplicationPermissionManifestCommand(
            application.Id.Value,
            "actor-1",
            application.ConcurrencyToken));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequestedManifestVersion.ShouldBe("1.1.0");
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyRemoteChangesAsync_WithNoManifestBaseUrl_ReturnsValidationError()
    {
        RegisteredApplication application = CreateApplication("1.0.0", manifestBaseUrl: null);
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<ManifestApplyDto> result = await this.useCases.ApplyRemoteChangesAsync(new RemoteApplicationPermissionManifestCommand(
            application.Id.Value,
            "actor-1",
            application.ConcurrencyToken));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.ManifestBaseUrlRequired");
        await this.remoteManifestFetcher.DidNotReceive().FetchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyChangesAsync_WithManualApplication_ReturnsValidationError()
    {
        RegisteredApplication application = CreateApplication("0.0.0", manifestBaseUrl: null);
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        Result<ManifestApplyDto> result = await this.useCases.ApplyChangesAsync(new ApplyApplicationPermissionManifestCommand(
            application.Id.Value,
            ValidManifest("1.0.0"),
            "actor-1",
            application.ConcurrencyToken));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionManifest.ManifestBaseUrlRequired");
        await this.repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private RegisteredApplication CreateApplication(string manifestVersion, string? manifestBaseUrl = "https://orders.example.com/api")
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            "Order management",
            "owner-1",
            OwnerType.User,
            [("order:read", "Read order", null, "Orders")],
            "actor-1",
            this.dateTimeProvider,
            schemaVersion: "1.0.0",
            manifestVersion: manifestVersion,
            manifestBaseUrl: manifestBaseUrl);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static PermissionManifestDocument ValidManifest(string version)
    {
        return new PermissionManifestDocument(
            "1.0.0",
            new PermissionManifestApplicationDeclaration("orders-api", "Orders API", "Order management", version),
            [new PermissionManifestPermissionDeclaration("order:read", "Read order", null, "Orders")]);
    }

    private static PermissionManifestDocument EmptyManifest(string version)
    {
        return new PermissionManifestDocument(
            "1.0.0",
            new PermissionManifestApplicationDeclaration("orders-api", "Orders API", "Order management", version),
            []);
    }
}
