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

    [Fact]
    public void AddUnifiedApplications_CreatesProfileColumnAndIndex()
    {
        IReadOnlyList<MigrationOperation> operations = GetUpOperations(typeof(AddUnifiedApplications));

        CreateTableOperation applications = operations
            .OfType<CreateTableOperation>()
            .Single(operation => operation.Name == "Applications");
        applications.Columns.Select(column => column.Name).ShouldContain("Profile");
        applications.Columns.Select(column => column.Name).ShouldNotContain("Type");

        CreateIndexOperation profileIndex = operations
            .OfType<CreateIndexOperation>()
            .Single(operation => operation.Table == "Applications" && operation.Name == "IX_Applications_Profile");
        profileIndex.Columns.ShouldBe(["Profile"]);
    }

    [Fact]
    public void RenameOpenIddictApplicationType_Migration_GuardsLegacyColumnRename()
    {
        Type? migrationType = typeof(OpenIdentityStackDbContext).Assembly.GetType(
            "OpenIdentityStack.Infrastructure.Persistence.Migrations.RenameOpenIddictApplicationType");
        migrationType.ShouldNotBeNull();

        IReadOnlyList<MigrationOperation> operations = GetUpOperations(migrationType!);
        operations.OfType<RenameColumnOperation>().ShouldBeEmpty();

        SqlOperation guardedRename = operations
            .OfType<SqlOperation>()
            .Single();

        guardedRename.Sql.ShouldContain("information_schema.columns");
        guardedRename.Sql.ShouldContain("OpenIddictApplications");
        guardedRename.Sql.ShouldContain("ApplicationProfile");
        guardedRename.Sql.ShouldContain("ApplicationType");
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
