using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteApplicationCredentialMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "ApplicationCredentials",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ApplicationCredentials",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "ApplicationCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastUsedAt",
                table: "ApplicationCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModifiedAt",
                table: "ApplicationCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "ApplicationCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecretHash",
                table: "ApplicationCredentials",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "ApplicationCredentials",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Thumbprint",
                table: "ApplicationCredentials",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ApplicationCredentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationCredentials_ApplicationId_Thumbprint",
                table: "ApplicationCredentials",
                columns: new[] { "ApplicationId", "Thumbprint" },
                unique: true,
                filter: "\"Thumbprint\" IS NOT NULL AND \"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationCredentials_ApplicationId_Type_RevokedAt",
                table: "ApplicationCredentials",
                columns: new[] { "ApplicationId", "Type", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationCredentials_ApplicationId_Thumbprint",
                table: "ApplicationCredentials");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationCredentials_ApplicationId_Type_RevokedAt",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "SecretHash",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "Thumbprint",
                table: "ApplicationCredentials");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ApplicationCredentials");
        }
    }
}
