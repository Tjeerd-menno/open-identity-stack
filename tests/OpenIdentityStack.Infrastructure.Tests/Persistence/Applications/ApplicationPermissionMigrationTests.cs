using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Applications;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Applications;

public sealed class ApplicationPermissionMigrationTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private OpenIdentityStackDbContext dbContext = null!;
    private ApplicationPermissionMigration migration = null!;

    public ApplicationPermissionMigrationTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await this.fixture.ClearAllDataAsync();
        this.dbContext = this.fixture.CreateDbContext();
        this.migration = new ApplicationPermissionMigration(this.dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task MigrateAsync_ReplacesLegacyClientAndServiceAccountPermissions()
    {
        Role role = CreateRole(
            "operators",
            [
                "clients:read",
                "clients:manage-secrets",
                "service-accounts:rotate-secret",
                "service-accounts:manage-certificates",
                "users:read"
            ]);
        await this.dbContext.Roles.AddAsync(role);
        await this.dbContext.SaveChangesAsync();

        ApplicationPermissionMigrationResult result = await this.migration.MigrateAsync();

        result.UpdatedRoles.ShouldBe(1);
        Role migrated = await this.dbContext.Roles.SingleAsync();
        migrated.Permissions.ShouldBe(
            [
                "applications:read",
                "applications:manage-credentials",
                "applications:manage-certificates",
                "users:read"
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task MigrateAsync_MapsWildcardsAndRemovesDuplicateApplicationPermissions()
    {
        Role role = CreateRole(
            "admins",
            [
                "clients:*",
                "service-accounts:*",
                "applications:*",
                "clients:delete"
            ]);
        await this.dbContext.Roles.AddAsync(role);
        await this.dbContext.SaveChangesAsync();

        await this.migration.MigrateAsync();

        Role migrated = await this.dbContext.Roles.SingleAsync();
        migrated.Permissions.Count(permission => permission == "applications:*").ShouldBe(1);
        migrated.Permissions.ShouldNotContain("clients:*");
        migrated.Permissions.ShouldNotContain("service-accounts:*");
    }

    private static Role CreateRole(string name, IReadOnlyList<string> permissions)
    {
        Result<Role> result = Role.Create(name, description: null);
        result.IsSuccess.ShouldBeTrue();

        Role role = result.Value;
        role.SetPermissions(permissions);
        return role;
    }
}
