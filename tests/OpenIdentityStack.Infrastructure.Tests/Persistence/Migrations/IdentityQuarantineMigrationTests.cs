using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Migrations;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Migrations;

public sealed class IdentityQuarantineMigrationTests
{
    [Fact]
    public async Task LegacyRowsRemainWithUnknownEvidenceWhenMigrationIsApplied()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<OpenIdentityStackDbContext> options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseSqlite(connection).Options;
        await using var db = new OpenIdentityStackDbContext(options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE UserUpstreamIdentities (Id INTEGER PRIMARY KEY, Issuer TEXT); INSERT INTO UserUpstreamIdentities (Id, Issuer) VALUES (1, 'https://historical.example')");
        var migration = new QuarantineUnprovenIdentityAssociations();
        IMigrationsSqlGenerator generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (MigrationCommand command in generator.Generate(migration.UpOperations))
        {
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        }
        await using SqliteCommand read = connection.CreateCommand();
        read.CommandText = "SELECT AssociationEvidence FROM UserUpstreamIdentities WHERE Id = 1";
        (await read.ExecuteScalarAsync()).ShouldBe("Unknown");
    }
}
