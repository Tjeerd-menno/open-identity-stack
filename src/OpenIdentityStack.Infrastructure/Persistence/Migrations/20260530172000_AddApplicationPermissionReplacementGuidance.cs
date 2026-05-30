using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OpenIdentityStackDbContext))]
    [Migration("20260530172000_AddApplicationPermissionReplacementGuidance")]
    public partial class AddApplicationPermissionReplacementGuidance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReplacementFullPermissionKey",
                table: "ApplicationPermissions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementNote",
                table: "ApplicationPermissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacementFullPermissionKey",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "ReplacementNote",
                table: "ApplicationPermissions");
        }
    }
}
