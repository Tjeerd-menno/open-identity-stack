using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.Tests.Common;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public class SeedDataTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _context = null!;

    public SeedDataTests(SqliteTestFixture fixture)
    {
        this._fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await this._fixture.ClearAllDataAsync();
        this._context = this._fixture.CreateDbContext();
    }

    public ValueTask DisposeAsync()
    {
        return this._context.DisposeAsync();
    }

    private OpenIdentityStackDbContext CreateContext() => this._fixture.CreateDbContext();

    [Fact]
    public async Task SeedAsync_ShouldCreateSystemRoles_WhenTheyDoNotExist()
    {
        // Arrange
        OpenIdentityStackDbContext context = this.CreateContext();

        // Act
        await SeedData.SeedAsync(context);

        // Assert
        List<Role> roles = await context.Roles.ToListAsync();
        roles.Count.ShouldBeGreaterThan(0);
        roles.ShouldContain(r => r.Name == SeedData.SystemRoles.SuperAdmin);
        roles.ShouldContain(r => r.Name == SeedData.SystemRoles.UserAdmin);

        Role superAdmin = roles.First(r => r.Name == SeedData.SystemRoles.SuperAdmin);
        superAdmin.IsSystemRole.ShouldBeTrue();
    }

    [Fact]
    public async Task SeedAsync_ShouldSkipExistingRoles_WhenTheyExist()
    {
        // Arrange
        OpenIdentityStackDbContext context = this.CreateContext();
        await SeedData.SeedAsync(context); // First run
        int initialCount = await context.Roles.CountAsync();

        // Act
        await SeedData.SeedAsync(context); // Second run

        // Assert
        int finalCount = await context.Roles.CountAsync();
        finalCount.ShouldBe(initialCount);
    }
}
