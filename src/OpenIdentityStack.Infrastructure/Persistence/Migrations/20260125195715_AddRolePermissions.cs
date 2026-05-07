using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Permissions",
                table: "Roles",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
            
            // Fix any existing rows that might have wrong default value
            migrationBuilder.Sql("UPDATE \"Roles\" SET \"Permissions\" = '[]' WHERE \"Permissions\" = '{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "Roles");
        }
    }
}
