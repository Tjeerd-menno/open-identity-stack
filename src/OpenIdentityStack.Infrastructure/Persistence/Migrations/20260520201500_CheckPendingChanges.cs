using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeprecatedAt",
                table: "ServicePermissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAt",
                table: "ServicePermissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetiredAt",
                table: "ServicePermissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServicePermissions_Status",
                table: "ServicePermissions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServicePermissions_Status",
                table: "ServicePermissions");

            migrationBuilder.DropColumn(
                name: "DeprecatedAt",
                table: "ServicePermissions");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "ServicePermissions");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "ServicePermissions");
        }
    }
}
