using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Migrations;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Applications;

public sealed class ApplicationMigrationSqlTests
{
    [Theory]
    [InlineData(typeof(BackfillClientsToApplications))]
    [InlineData(typeof(BackfillServiceAccountsToApplications))]
    public void BackfillMigrations_GenerateNewApplicationIds(Type migrationType)
    {
        string sql = string.Join(
            Environment.NewLine,
            GetUpOperations(migrationType).OfType<SqlOperation>().Select(operation => operation.Sql));

        sql.ShouldContain("gen_random_uuid()");
        sql.ShouldNotContain("c.\"Id\",");
        sql.ShouldNotContain("s.\"Id\",");
    }

    [Fact]
    public void CleanupMigration_DropsLegacyApplicationTablesInDependencyOrder()
    {
        Type? migrationType = typeof(OpenIdentityStackDbContext).Assembly.GetType(
            "OpenIdentityStack.Infrastructure.Persistence.Migrations.DropLegacyApplicationTables");
        migrationType.ShouldNotBeNull();

        string[] droppedTables = GetUpOperations(migrationType!)
            .OfType<DropTableOperation>()
            .Select(operation => operation.Name)
            .ToArray();

        droppedTables.ShouldBe(
        [
            "ClientCertificates",
            "ClientCredentials",
            "ServiceAccounts",
            "Clients"
        ]);
    }

    private static IReadOnlyList<MigrationOperation> GetUpOperations(Type migrationType)
    {
        var migration = (Migration)Activator.CreateInstance(migrationType)!;
        var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        MethodInfo upMethod = typeof(Migration).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Migration Up method could not be resolved.");
        upMethod.Invoke(migration, [migrationBuilder]);
        return migrationBuilder.Operations;
    }
}
