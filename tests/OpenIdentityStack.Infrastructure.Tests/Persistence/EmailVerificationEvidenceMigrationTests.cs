using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class EmailVerificationEvidenceMigrationTests
{
    [Fact]
    public void BackfillsIndependentEvidenceOnlyForPreviouslyVerifiedLocalUsers()
    {
        Type migrationType = typeof(OpenIdentityStackDbContext).Assembly.GetType(
            "OpenIdentityStack.Infrastructure.Persistence.Migrations.RecordIndependentEmailVerificationEvidence")!;
        var migration = (Migration)Activator.CreateInstance(migrationType)!;
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        MethodInfo up = typeof(Migration).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!;

        up.Invoke(migration, [builder]);

        string sql = builder.Operations.OfType<SqlOperation>().Single().Sql;
        sql.ShouldContain("INSERT INTO \"UserEmailVerificationEvidence\"");
        sql.ShouldContain("\"Status\" <> 0");
        sql.ShouldContain("\"PasswordHash\" IS NOT NULL");
        sql.ShouldContain("\"NormalizedEmail\"");
    }

    [Fact]
    public void SubsequentMigrationRetainsIndependentEmailVerificationEvidenceMapping()
    {
        var migration = new OpenIdentityStack.Infrastructure.Persistence.Migrations.BindFederationIssuers();

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? evidence = migration.TargetModel
            .GetEntityTypes()
            .SingleOrDefault(entityType => entityType.GetTableName() == "UserEmailVerificationEvidence");

        evidence.ShouldNotBeNull();
        evidence.GetTableName().ShouldBe("UserEmailVerificationEvidence");
        evidence.FindProperty(nameof(EmailVerificationEvidence.NormalizedEmail)).ShouldNotBeNull();
        migration.TargetModel.FindEntityType(typeof(User))!
            .FindNavigation(nameof(User.EmailVerificationEvidence))
            .ShouldNotBeNull();
    }
}
