using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationPermissionTombstones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "RegisteredApplications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "RegisteredApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "RegisteredApplications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemoveReason",
                table: "ApplicationPermissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedAt",
                table: "ApplicationPermissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemovedBy",
                table: "ApplicationPermissions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "RegisteredApplications");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "RegisteredApplications");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "RegisteredApplications");

            migrationBuilder.DropColumn(
                name: "RemoveReason",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "RemovedBy",
                table: "ApplicationPermissions");
        }
    }
}
