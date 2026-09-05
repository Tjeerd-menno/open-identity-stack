using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordEmailVerificationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "email_trust_version",
                table: "upstream_providers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "trust_email_verification",
                table: "upstream_providers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.DropIndex(
                name: "IX_UserEmailVerificationEvidence_UserId",
                table: "UserEmailVerificationEvidence");

            migrationBuilder.AddColumn<string>(
                name: "Issuer",
                table: "UserEmailVerificationEvidence",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProviderId",
                table: "UserEmailVerificationEvidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WithdrawnAt",
                table: "UserEmailVerificationEvidence",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserEmailVerificationEvidence_UserId_ProviderId",
                table: "UserEmailVerificationEvidence",
                columns: new[] { "UserId", "ProviderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserEmailVerificationEvidence_UserId_ProviderId",
                table: "UserEmailVerificationEvidence");

            migrationBuilder.DropColumn(
                name: "Issuer",
                table: "UserEmailVerificationEvidence");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "UserEmailVerificationEvidence");

            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                table: "UserEmailVerificationEvidence");

            migrationBuilder.CreateIndex(
                name: "IX_UserEmailVerificationEvidence_UserId",
                table: "UserEmailVerificationEvidence",
                column: "UserId");

            migrationBuilder.DropColumn(
                name: "email_trust_version",
                table: "upstream_providers");

            migrationBuilder.DropColumn(
                name: "trust_email_verification",
                table: "upstream_providers");
        }
    }
}
