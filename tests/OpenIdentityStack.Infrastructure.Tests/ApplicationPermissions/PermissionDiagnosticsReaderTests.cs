using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.ApplicationPermissions;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;

namespace OpenIdentityStack.Infrastructure.Tests.ApplicationPermissions;

public sealed class PermissionDiagnosticsReaderTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private readonly IDateTimeProvider dateTimeProvider;
    private OpenIdentityStackDbContext dbContext = null!;
    private PermissionDiagnosticsReader reader = null!;

    public PermissionDiagnosticsReaderTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
    }

    public async ValueTask InitializeAsync()
    {
        await this.fixture.ClearAllDataAsync();
        this.dbContext = this.fixture.CreateDbContext();
        this.reader = new PermissionDiagnosticsReader(this.dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ListIssuesAsync_DoesNotReportPlatformWildcardPermissions()
    {
        Role role = Role.Create("platform-admin", "Platform Admin", null).Value;
        role.SetPermissions([
            Permissions.Users.All,
            Permissions.Roles.All,
            Permissions.Groups.All,
            Permissions.ApplicationPermissions.All,
            Permissions.Sessions.All,
            Permissions.Providers.All,
            Permissions.Applications.All,
            Permissions.AuditLogs.All,
            Permissions.System.All,
            Permissions.All,
        ]);

        this.dbContext.Roles.Add(role);
        await this.dbContext.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        PermissionDiagnosticsDto result = await this.reader.ListIssuesAsync();

        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListIssuesAsync_ReportsMalformedUnknownOrphanedDeletedTombstonedAndCollapsedWildcardAssignments()
    {
        RegisteredApplication activeApplication = CreateApplication("orders-api", [("order:read", "Read orders", null, null)]);
        RegisteredApplication removedPermissionApplication = CreateApplication("claims-api", [("claim:read", "Read claims", null, null)]);
        ApplicationPermission removedPermission = removedPermissionApplication.Permissions.Single();
        removedPermissionApplication.RemovePermission(removedPermission.Id, "admin-1", "Superseded.", this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        RegisteredApplication deletedApplication = CreateApplication("billing-api", [("invoice:read", "Read invoices", null, null)]);
        deletedApplication.MarkDeleted("admin-1", "Retired.", this.dateTimeProvider);
        Role role = Role.Create("diagnostic-role", "Diagnostic Role", null).Value;
        role.SetPermissions([
            "not-valid",
            "unknown-api:order:read",
            "orders-api:order:write",
            "billing-api:invoice:read",
            "claims-api:claim:read",
            "orders-api:invoice:*",
            "orders-api:order:read",
            "users:read",
        ]);

        this.dbContext.RegisteredApplications.AddRange(activeApplication, removedPermissionApplication, deletedApplication);
        this.dbContext.Roles.Add(role);
        await this.dbContext.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        PermissionDiagnosticsDto result = await this.reader.ListIssuesAsync();

        result.Issues.Select(issue => issue.Kind).ShouldBe(
            ["malformed", "unknown", "orphaned", "deletedApplication", "tombstonedPermission", "collapsedWildcard"],
            ignoreOrder: true);
        result.Issues.ShouldNotContain(issue => issue.Permission == "orders-api:order:read");
        result.Issues.ShouldNotContain(issue => issue.Permission == "users:read");
    }

    private RegisteredApplication CreateApplication(
        string applicationIdentifier,
        IReadOnlyList<(string Key, string DisplayName, string? Description, string? Category)> permissions)
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            applicationIdentifier,
            applicationIdentifier,
            null,
            "owner-1",
            OwnerType.User,
            permissions,
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
