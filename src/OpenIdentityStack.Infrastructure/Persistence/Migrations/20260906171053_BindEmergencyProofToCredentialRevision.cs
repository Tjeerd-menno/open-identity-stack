using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BindEmergencyProofToCredentialRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CredentialRevision",
                table: "EmergencyAccessEvidence",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialRevision",
                table: "EmergencyAccessEvidence");
        }
    }
}
