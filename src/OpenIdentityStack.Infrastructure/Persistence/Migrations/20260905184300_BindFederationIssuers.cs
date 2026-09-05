using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BindFederationIssuers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Issuer",
                table: "UserUpstreamIdentities",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bound_issuer",
                table: "upstream_providers",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "identity_configuration_locked",
                table: "upstream_providers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "identity_version",
                table: "upstream_providers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            // Lock legacy providers without inventing issuer or account-control evidence.
            migrationBuilder.Sql("""
                UPDATE upstream_providers SET identity_configuration_locked = TRUE
                WHERE EXISTS (SELECT 1 FROM "UserUpstreamIdentities" i WHERE i."ProviderId" = upstream_providers.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Issuer",
                table: "UserUpstreamIdentities");

            migrationBuilder.DropColumn(
                name: "bound_issuer",
                table: "upstream_providers");

            migrationBuilder.DropColumn(
                name: "identity_configuration_locked",
                table: "upstream_providers");

            migrationBuilder.DropColumn(
                name: "identity_version",
                table: "upstream_providers");
        }
    }
}
