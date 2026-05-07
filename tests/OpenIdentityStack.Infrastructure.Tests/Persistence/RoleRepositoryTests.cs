using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Roles;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class RoleRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _dbContext = null!;
    private RoleRepository _repository = null!;

    public RoleRepositoryTests(SqliteTestFixture fixture)
    {
        this._fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Clear data before each test runs to ensure isolation
        await this._fixture.ClearAllDataAsync();

        this._dbContext = this._fixture.CreateDbContext();
        this._repository = new RoleRepository(this._dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this._dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsRole_WhenExists()
    {
        Role role = await this.SeedAsync("test-role", "Test Role");

        Role? result = await this._repository.GetByIdAsync(role.Id);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("test-role");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        Role? result = await this._repository.GetByIdAsync(RoleId.Create());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsRole_WhenExists()
    {
        await this.SeedAsync("test-role", "Test Role");

        Role? result = await this._repository.GetByNameAsync("test-role");

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("test-role");
    }

    [Fact]
    public async Task GetByNameAsync_IsNormalized()
    {
        await this.SeedAsync("test-role", "Test Role");

        Role? result = await this._repository.GetByNameAsync("TEST-ROLE");

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("test-role");
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenNotExists()
    {
        Role? result = await this._repository.GetByNameAsync("missing");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsTrue_WhenExists()
    {
        await this.SeedAsync("test-role", "Test Role");

        bool exists = await this._repository.ExistsByNameAsync("test-role");

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsFalse_WhenNotExists()
    {
        bool exists = await this._repository.ExistsByNameAsync("missing");

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsActiveRoles_ByDefault()
    {
        await this.SeedAsync("active-role", "Active Role");
        Role inactive = await this.SeedAsync("inactive-role", "Inactive Role");
        inactive.Disable();
        await this._repository.SaveChangesAsync();

        IReadOnlyList<Role> result = await this._repository.GetAllAsync();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("active-role");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllRoles_WhenIncludeInactive()
    {
        await this.SeedAsync("active-role", "Active Role");
        Role inactive = await this.SeedAsync("inactive-role", "Inactive Role");
        inactive.Disable();
        await this._repository.SaveChangesAsync();

        IReadOnlyList<Role> result = await this._repository.GetAllAsync(includeInactive: true);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsActiveCount_ByDefault()
    {
        await this.SeedAsync("active-role", "Active Role");
        Role inactive = await this.SeedAsync("inactive-role", "Inactive Role");
        inactive.Disable();
        await this._repository.SaveChangesAsync();

        int count = await this._repository.GetCountAsync();

        count.ShouldBe(1);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsAllCount_WhenIncludeInactive()
    {
        await this.SeedAsync("active-role", "Active Role");
        Role inactive = await this.SeedAsync("inactive-role", "Inactive Role");
        inactive.Disable();
        await this._repository.SaveChangesAsync();

        int count = await this._repository.GetCountAsync(includeInactive: true);

        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllAsync_WithSearch_FiltersResultsByName()
    {
        await this.SeedAsync("admin-role", "Admin Role");
        await this.SeedAsync("user-role", "User Role");
        await this.SeedAsync("guest-role", "Guest Role");

        IReadOnlyList<Role> result = await this._repository.GetAllAsync(includeInactive: true, search: "admin");

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("admin-role");
    }

    [Fact]
    public async Task GetAllAsync_WithSearch_FiltersResultsByDisplayName()
    {
        await this.SeedAsync("role-a", "Administrator");
        await this.SeedAsync("role-b", "User Manager");
        await this.SeedAsync("role-c", "Guest Viewer");

        IReadOnlyList<Role> result = await this._repository.GetAllAsync(includeInactive: true, search: "Manager");

        result.Count.ShouldBe(1);
        result[0].DisplayName.ShouldBe("User Manager");
    }

    [Fact]
    public async Task GetAllAsync_WithSearch_IsCaseInsensitive()
    {
        await this.SeedAsync("admin-role", "Admin Role");

        IReadOnlyList<Role> lowerResult = await this._repository.GetAllAsync(includeInactive: true, search: "admin");
        IReadOnlyList<Role> upperResult = await this._repository.GetAllAsync(includeInactive: true, search: "ADMIN");
        IReadOnlyList<Role> mixedResult = await this._repository.GetAllAsync(includeInactive: true, search: "AdMiN");

        lowerResult.Count.ShouldBe(1);
        upperResult.Count.ShouldBe(1);
        mixedResult.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetCountAsync_WithSearch_ReturnsFilteredCount()
    {
        await this.SeedAsync("admin-role", "Admin Role");
        await this.SeedAsync("user-role", "User Role");
        await this.SeedAsync("guest-role", "Guest Role");

        int count = await this._repository.GetCountAsync(includeInactive: true, search: "admin");

        count.ShouldBe(1);
    }

    [Fact]
    public async Task AddAsync_AddsRole()
    {
        Role role = Role.Create("new-role", "New Role").Value;

        await this._repository.AddAsync(role);
        await this._repository.SaveChangesAsync();

        Role? saved = await this._dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "new-role");
        saved.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetUserRolesAsync_ReturnsUserRoles()
    {
        Role role1 = await this.SeedAsync("role1", "Role 1");
        Role role2 = await this.SeedAsync("role2", "Role 2");
        UserId userId = UserId.Create();

        RoleAssignment assignment1 = RoleAssignment.Create(userId, role1.Id, DateTimeOffset.UtcNow).Value;
        RoleAssignment assignment2 = RoleAssignment.Create(userId, role2.Id, DateTimeOffset.UtcNow).Value;
        await this._repository.AssignRoleAsync(assignment1);
        await this._repository.AssignRoleAsync(assignment2);
        await this._repository.SaveChangesAsync();

        IReadOnlyList<Role> result = await this._repository.GetUserRolesAsync(userId);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task IsRoleAssignedAsync_ReturnsTrue_WhenAssigned()
    {
        Role role = await this.SeedAsync("test-role", "Test Role");
        UserId userId = UserId.Create();

        RoleAssignment assignment = RoleAssignment.Create(userId, role.Id, DateTimeOffset.UtcNow).Value;
        await this._repository.AssignRoleAsync(assignment);
        await this._repository.SaveChangesAsync();

        bool result = await this._repository.IsRoleAssignedAsync(userId, role.Id);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsRoleAssignedAsync_ReturnsFalse_WhenNotAssigned()
    {
        Role role = await this.SeedAsync("test-role", "Test Role");
        UserId userId = UserId.Create();

        bool result = await this._repository.IsRoleAssignedAsync(userId, role.Id);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveRoleAssignmentAsync_RemovesAssignment()
    {
        Role role = await this.SeedAsync("test-role", "Test Role");
        UserId userId = UserId.Create();

        RoleAssignment assignment = RoleAssignment.Create(userId, role.Id, DateTimeOffset.UtcNow).Value;
        await this._repository.AssignRoleAsync(assignment);
        await this._repository.SaveChangesAsync();

        await this._repository.RemoveRoleAssignmentAsync(userId, role.Id);
        await this._repository.SaveChangesAsync();

        bool exists = await this._repository.IsRoleAssignedAsync(userId, role.Id);
        exists.ShouldBeFalse();
    }

    private static Role CreateRole(string name, string? displayName = null, string? description = null)
    {
        return Role.Create(name, displayName, description).Value;
    }

    private async Task<Role> SeedAsync(string name, string? displayName = null, string? description = null)
    {
        Role role = CreateRole(name, displayName, description);
        await this._repository.AddAsync(role);
        await this._repository.SaveChangesAsync();
        return role;
    }
}
