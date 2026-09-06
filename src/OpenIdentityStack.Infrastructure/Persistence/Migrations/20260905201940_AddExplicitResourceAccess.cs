using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExplicitResourceAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProtectedResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Audience = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    PermissionNamespaces = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtectedResources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientResourceGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ApplicationPermissions = table.Column<string>(type: "jsonb", nullable: false),
                    DelegatedPermissions = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientResourceGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientResourceGrants_Applications_ClientApplicationId",
                        column: x => x.ClientApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientResourceGrants_ProtectedResources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "ProtectedResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientResourceGrants_ClientApplicationId_ResourceId",
                table: "ClientResourceGrants",
                columns: new[] { "ClientApplicationId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientResourceGrants_ResourceId",
                table: "ClientResourceGrants",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedResources_Audience",
                table: "ProtectedResources",
                column: "Audience",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProtectedResources_Scope",
                table: "ProtectedResources",
                column: "Scope",
                unique: true);

            // Existing OAuth scope/resource strings are not evidence of a permission grant.
            // Preserve registrations for an explicit operator-reviewed mapping/ceiling migration.
            migrationBuilder.Sql("""
                UPDATE "Applications"
                SET "RequiresMigrationReview" = TRUE,
                    "MigrationSource" = CASE
                        WHEN "RequiresMigrationReview" THEN "MigrationSource"
                        ELSE 'resource-access-boundary-v1'
                    END
                WHERE EXISTS (
                    SELECT 1 FROM jsonb_array_elements_text("AllowedScopes") AS scope(value)
                    WHERE scope.value NOT IN ('openid', 'profile', 'email', 'address', 'phone', 'offline_access', 'roles')
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientResourceGrants");

            migrationBuilder.DropTable(
                name: "ProtectedResources");
        }
    }
}
