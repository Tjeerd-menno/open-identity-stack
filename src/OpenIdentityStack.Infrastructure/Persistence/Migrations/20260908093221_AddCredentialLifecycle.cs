using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCredentialLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "UserSecurityVersion",
                table: "UserSessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "SecurityVersion",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "LogoutAttemptCount",
                table: "ClientSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LogoutCompletedAt",
                table: "ClientSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LogoutStatus",
                table: "ClientSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextLogoutAttemptAt",
                table: "ClientSessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserSecurityVersion",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "SecurityVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LogoutAttemptCount",
                table: "ClientSessions");

            migrationBuilder.DropColumn(
                name: "LogoutCompletedAt",
                table: "ClientSessions");

            migrationBuilder.DropColumn(
                name: "LogoutStatus",
                table: "ClientSessions");

            migrationBuilder.DropColumn(
                name: "NextLogoutAttemptAt",
                table: "ClientSessions");
        }
    }
}
