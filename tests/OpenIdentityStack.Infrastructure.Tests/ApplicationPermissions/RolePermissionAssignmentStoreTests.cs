using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.ApplicationPermissions;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.ApplicationPermissions;

public sealed class RolePermissionAssignmentStoreTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private OpenIdentityStackDbContext dbContext = null!;
    private RolePermissionAssignmentStore store = null!;

    public RolePermissionAssignmentStoreTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await this.fixture.ClearAllDataAsync();
        this.dbContext = this.fixture.CreateDbContext();
        this.store = new RolePermissionAssignmentStore(this.dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task PreviewRemovalImpactAsync_ReturnsExactWildcardAndImpactedRoleAssignments()
    {
        Role exactRole = CreateRole("Order cancelers", ["orders-api:order:cancel"]);
        Role wildcardRole = CreateRole("Order admins", ["orders-api:order:*"]);
        Role unrelatedRole = CreateRole("Readers", ["orders-api:order:read"]);
        this.dbContext.Roles.AddRange(exactRole, wildcardRole, unrelatedRole);
        await this.dbContext.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        var plan = new PermissionAssignmentRemovalPlan(
            ["orders-api:order:cancel"],
            ["orders-api:order:*"]);

        IReadOnlyList<PermissionAssignmentImpactDto> impacts = await this.store.PreviewRemovalImpactAsync(plan);

        impacts.Select(impact => (impact.AssignmentDisplayName, impact.Permission, impact.ImpactKind)).ShouldBe(
            [
                ("Order cancelers", "orders-api:order:cancel", "exactRemoved"),
                ("Order admins", "orders-api:order:*", "wildcardRemoved"),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task PreviewRemovalImpactAsync_ReturnsWildcardImpactedWhenWildcardGrantIsNotRemoved()
    {
        Role wildcardRole = CreateRole("Order admins", ["orders-api:order:*"]);
        this.dbContext.Roles.Add(wildcardRole);
        await this.dbContext.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        var plan = new PermissionAssignmentRemovalPlan(
            ["orders-api:order:cancel"],
            []);

        IReadOnlyList<PermissionAssignmentImpactDto> impacts = await this.store.PreviewRemovalImpactAsync(plan);

        impacts.ShouldHaveSingleItem();
        impacts[0].AssignmentDisplayName.ShouldBe("Order admins");
        impacts[0].Permission.ShouldBe("orders-api:order:*");
        impacts[0].ImpactKind.ShouldBe("wildcardImpacted");
    }

    [Fact]
    public async Task RemoveAssignmentsAsync_RemovesExactAndWildcardAssignmentsFromRoles()
    {
        Role exactRole = CreateRole("Order cancelers", ["orders-api:order:cancel", "orders-api:order:read"]);
        Role wildcardRole = CreateRole("Order admins", ["orders-api:order:*", "users:read"]);
        Role impactedRole = CreateRole("Order viewers", ["orders-api:order:*"]);
        this.dbContext.Roles.AddRange(exactRole, wildcardRole, impactedRole);
        await this.dbContext.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        var plan = new PermissionAssignmentRemovalPlan(
            ["orders-api:order:cancel"],
            ["orders-api:order:*"]);

        Result<IReadOnlyList<PermissionAssignmentImpactDto>> result = await this.store.RemoveAssignmentsAsync(plan, "admin-1");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(impact => impact.ImpactKind).ShouldBe(["exactRemoved", "wildcardRemoved", "wildcardRemoved"], ignoreOrder: true);
        var roles = this.dbContext.Roles.OrderBy(role => role.Name).ToList();
        roles.Single(role => role.DisplayName == "Order cancelers").Permissions.ShouldBe(["orders-api:order:read"]);
        roles.Single(role => role.DisplayName == "Order admins").Permissions.ShouldBe(["users:read"]);
        roles.Single(role => role.DisplayName == "Order viewers").Permissions.ShouldBeEmpty();
    }

    private static Role CreateRole(string name, IReadOnlyList<string> permissions)
    {
        Role role = Role.Create(name, null).Value;
        role.SetPermissions(permissions);
        return role;
    }
}
