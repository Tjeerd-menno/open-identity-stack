using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class IndexActiveProviderEmailEvidence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateIndex(
        name: "IX_EmailEvidence_ActiveProviderUser",
        table: "UserEmailVerificationEvidence",
        columns: new[] { "ProviderId", "UserId" },
        filter: "\"WithdrawnAt\" IS NULL");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropIndex(
        name: "IX_EmailEvidence_ActiveProviderUser",
        table: "UserEmailVerificationEvidence");
}
