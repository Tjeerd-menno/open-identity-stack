using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationPermissionManifestMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManifestBaseUrl",
                table: "RegisteredApplications",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManifestVersion",
                table: "RegisteredApplications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SchemaVersion",
                table: "RegisteredApplications",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManifestBaseUrl",
                table: "RegisteredApplications");

            migrationBuilder.DropColumn(
                name: "ManifestVersion",
                table: "RegisteredApplications");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "RegisteredApplications");
        }
    }
}
