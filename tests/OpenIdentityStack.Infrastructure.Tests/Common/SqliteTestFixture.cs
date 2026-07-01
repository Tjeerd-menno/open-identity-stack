using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Tests.Common;

/// <summary>
/// Class-level fixture that provides a fresh SQLite database for each test class.
/// Each test class gets its own isolated in-memory database to avoid concurrency issues.
/// </summary>
public sealed class SqliteTestFixture : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    /// <summary>
    /// Gets the DbContext options configured for SQLite.
    /// </summary>
    public DbContextOptions<OpenIdentityStackDbContext> Options { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // In-memory SQLite database - connection must stay open
        this._connection = new SqliteConnection("DataSource=:memory:");
        await this._connection.OpenAsync();

        this.Options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>()
            .UseSqlite(this._connection)
            .Options;

        // Create the schema
        await using OpenIdentityStackDbContext context = new(this.Options);
        await context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await this._connection.CloseAsync();
        await this._connection.DisposeAsync();
    }

    /// <summary>
    /// Creates a new DbContext instance for use in tests.
    /// </summary>
    public OpenIdentityStackDbContext CreateDbContext() => new(this.Options);

    /// <summary>
    /// Clears all data from the database tables.
    /// Call this in test class InitializeAsync to ensure isolation between tests.
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        await using OpenIdentityStackDbContext context = new(this.Options);

        // Delete in correct order to respect foreign key constraints
        context.DelegatedMaintainers.RemoveRange(await context.DelegatedMaintainers.ToListAsync());
        context.ApplicationCredentials.RemoveRange(await context.ApplicationCredentials.ToListAsync());
        context.Applications.RemoveRange(await context.Applications.ToListAsync());
        context.ApplicationPermissions.RemoveRange(await context.ApplicationPermissions.ToListAsync());
        context.RegisteredApplications.RemoveRange(await context.RegisteredApplications.ToListAsync());
        context.RoleAssignments.RemoveRange(await context.RoleAssignments.ToListAsync());
        context.UserSessions.RemoveRange(await context.UserSessions.ToListAsync());
        context.Groups.RemoveRange(await context.Groups.ToListAsync());
        context.Roles.RemoveRange(await context.Roles.ToListAsync());
        context.Users.RemoveRange(await context.Users.ToListAsync());
        context.UpstreamProviders.RemoveRange(await context.UpstreamProviders.ToListAsync());
        context.AuthenticationSettings.RemoveRange(await context.AuthenticationSettings.ToListAsync());
        context.AuditLogEntries.RemoveRange(await context.AuditLogEntries.ToListAsync());

        await context.SaveChangesAsync();
    }
}
