using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Tests.Common;

/// <summary>Runs the same federation policy races on SQLite by default, or an explicitly selected PostgreSQL server.</summary>
public sealed class FederationPolicyTestFixture : IAsyncLifetime
{
    private readonly SqliteTestFixture sqlite = new();
    private DbContextOptions<OpenIdentityStackDbContext>? postgres;

    public async ValueTask InitializeAsync()
    {
        string? connection = Environment.GetEnvironmentVariable("OIS_FEDERATION_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection))
        {
            await this.sqlite.InitializeAsync();
            return;
        }

        // Always create a distinct fixture-owned database. Never create/drop the supplied database.
        var isolated = new NpgsqlConnectionStringBuilder(connection)
        {
            Database = $"ois_federation_test_{Guid.NewGuid():N}",
            Pooling = false,
        };
        this.postgres = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseNpgsql(isolated.ConnectionString).Options;
        await using OpenIdentityStackDbContext db = this.CreateDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public bool IsPostgres => this.postgres is not null;

    public OpenIdentityStackDbContext CreateDbContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<OpenIdentityStackDbContext>(this.postgres ?? this.sqlite.Options)
            .AddInterceptors(interceptors).Options);

    public async ValueTask DisposeAsync()
    {
        if (this.postgres is null)
        {
            await this.sqlite.DisposeAsync();
            return;
        }

        await using OpenIdentityStackDbContext db = this.CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }
}
