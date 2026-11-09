using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Validators;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.ApplicationPermissions;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.ApplicationPermissions;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionTransactionRunnerTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private readonly IDateTimeProvider dateTimeProvider;
    private OpenIdentityStackDbContext dbContext = null!;
    private ApplicationPermissionTransactionRunner runner = null!;

    public ApplicationPermissionTransactionRunnerTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
    }

    public async ValueTask InitializeAsync()
    {
        await this.fixture.ClearAllDataAsync();
        this.dbContext = this.fixture.CreateDbContext();
        this.runner = new ApplicationPermissionTransactionRunner(this.dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationThrowsAfterSaves_RollsBackRegistryAssignmentsManifestAndAudit()
    {
        RegisteredApplication application = CreateApplication();
        Role role = Role.Create("Order cancelers", null).Value;
        role.SetPermissions(["orders-api:order:cancel"]);
        this.dbContext.RegisteredApplications.Add(application);
        this.dbContext.Roles.Add(role);
        await this.dbContext.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await this.runner.ExecuteAsync<IReadOnlyList<string>>(
                async cancellationToken =>
                {
                    RegisteredApplication persistedApplication = this.dbContext.RegisteredApplications
                        .Single(app => app.ApplicationIdentifier == "orders-api");
                    persistedApplication.RemovePermission(
                        persistedApplication.Permissions.Single().Id,
                        "admin-1",
                        "Permission removed.",
                        this.dateTimeProvider).IsSuccess.ShouldBeTrue();
                    persistedApplication.ApplyManifestMetadata(
                        "Orders API",
                        null,
                        "1.0.0",
                        "2.0.0",
                        "admin-1",
                        this.dateTimeProvider).IsSuccess.ShouldBeTrue();

                    Role persistedRole = this.dbContext.Roles.Single(existingRole => existingRole.Name == "order cancelers");
                    persistedRole.SetPermissions([]);

                    this.dbContext.AuditLogEntries.Add(new AuditLogEntry
                    {
                        UserId = "admin-1",
                        Action = "ApplyApplicationPermissionManifest",
                        EntityType = "RegisteredApplication",
                        EntityId = persistedApplication.Id.Value.ToString(),
                        Details = "Succeeded",
                        Timestamp = this.dateTimeProvider.UtcNow,
                    });
                    await this.dbContext.SaveChangesAsync(cancellationToken);
                    throw new InvalidOperationException("Simulated post-save failure.");
                },
                CancellationToken.None));
        this.dbContext.ChangeTracker.Clear();

        RegisteredApplication reloadedApplication = this.dbContext.RegisteredApplications.Single(app => app.ApplicationIdentifier == "orders-api");
        reloadedApplication.ManifestVersion.ShouldBe("1.0.0");
        this.dbContext.ApplicationPermissions.Single().IsRemoved.ShouldBeFalse();
        this.dbContext.Roles.Single(existingRole => existingRole.Name == "order cancelers").Permissions.ShouldBe(["orders-api:order:cancel"]);
        this.dbContext.AuditLogEntries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyChangesAsync_WhenAuthorizationDenied_PersistsDeniedAuditAfterRollback(bool canManageApplication)
    {
        RegisteredApplication application = CreateApplication();
        Role role = Role.Create("Order cancelers", null).Value;
        role.SetPermissions(["orders-api:order:cancel"]);
        this.dbContext.RegisteredApplications.Add(application);
        this.dbContext.Roles.Add(role);
        await this.dbContext.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        IApplicationPermissionAuthorizationService authorization = Substitute.For<IApplicationPermissionAuthorizationService>();
        authorization.CanManageApplicationAsync("actor-1", Arg.Any<RegisteredApplication>(), Arg.Any<CancellationToken>())
            .Returns(canManageApplication);
        IAdministrativeActorContext actorContext = Substitute.For<IAdministrativeActorContext>();
        actorContext.AuditActorId.Returns("actor-1");
        var auditLog = new AuditLogService(NullLogger<AuditLogService>.Instance, this.dbContext, this.dateTimeProvider);
        var useCases = new ApplicationPermissionManifestUseCases(
            new ApplicationPermissionRegistryRepository(this.dbContext),
            authorization,
            new ApplicationPermissionAuditWriter(auditLog, actorContext),
            this.dateTimeProvider,
            new RolePermissionAssignmentStore(this.dbContext),
            this.runner);
        var manifest = new PermissionManifestDocument(
            "1.0.0",
            new PermissionManifestApplicationDeclaration("orders-api", "Orders API", null, "2.0.0"),
            []);

        Result<ManifestApplyDto> result = await useCases.ApplyChangesAsync(
            new ApplyApplicationPermissionManifestCommand(application.Id.Value, manifest, "actor-1", application.ConcurrencyToken));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Forbidden.PermissionManifest.Forbidden");
        await using OpenIdentityStackDbContext freshContext = this.fixture.CreateDbContext();
        List<AuditLogEntry> audits = await freshContext.AuditLogEntries.ToListAsync();
        audits.Count.ShouldBe(1);
        AuditLogEntry audit = audits.Single();
        audit.Action.ShouldBe("ApplyApplicationPermissionManifest");
        audit.UserId.ShouldBe("actor-1");
        audit.EntityId.ShouldBe(application.Id.Value.ToString());
        audit.Details.ShouldBe("Denied");
        (await freshContext.RegisteredApplications.SingleAsync()).ManifestVersion.ShouldBe("1.0.0");
        (await freshContext.ApplicationPermissions.SingleAsync()).IsRemoved.ShouldBeFalse();
        (await freshContext.Roles.SingleAsync()).Permissions.ShouldBe(["orders-api:order:cancel"]);
    }

    private RegisteredApplication CreateApplication()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [("order:cancel", "Cancel orders", null, null)],
            "actor-1",
            this.dateTimeProvider,
            manifestVersion: "1.0.0",
            manifestBaseUrl: "https://orders.example.com/api");

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
