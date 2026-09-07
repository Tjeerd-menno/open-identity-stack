using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
}
