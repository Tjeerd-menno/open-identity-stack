using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.ApplicationPermissions;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
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
            manifestVersion: "1.0.0");

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
