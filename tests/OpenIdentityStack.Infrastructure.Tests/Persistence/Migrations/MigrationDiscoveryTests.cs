using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Migrations;

public sealed class MigrationDiscoveryTests : IClassFixture<SqliteTestFixture>
{
    private readonly SqliteTestFixture fixture;

    public MigrationDiscoveryTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Migrations_IncludesApplicationPermissionReplacementGuidance()
    {
        using OpenIdentityStackDbContext context = this.fixture.CreateDbContext();

        context.Database.GetMigrations()
            .ShouldContain("20260530172000_AddApplicationPermissionReplacementGuidance");
    }

    [Fact]
    public void Migrations_IncludesEmergencyProofCredentialRevisionBinding()
    {
        using OpenIdentityStackDbContext context = this.fixture.CreateDbContext();

        context.Database.GetMigrations()
            .ShouldContain("20260906171053_BindEmergencyProofToCredentialRevision");
    }
}
