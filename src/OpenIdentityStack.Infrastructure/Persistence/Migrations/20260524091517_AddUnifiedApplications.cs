using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnifiedApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Profile = table.Column<int>(type: "integer", nullable: false),
                    ClientType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequirePkce = table.Column<bool>(type: "boolean", nullable: false),
                    RequireConsent = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresMigrationReview = table.Column<bool>(type: "boolean", nullable: false),
                    MigrationSource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AllowedGrantTypes = table.Column<string>(type: "jsonb", nullable: false),
                    AllowedScopes = table.Column<string>(type: "jsonb", nullable: false),
                    PostLogoutRedirectUris = table.Column<string>(type: "jsonb", nullable: false),
                    RedirectUris = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationCredentials_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationCredentials_ApplicationId",
                table: "ApplicationCredentials",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_ClientId",
                table: "Applications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Status",
                table: "Applications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Profile",
                table: "Applications",
                column: "Profile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationCredentials");

            migrationBuilder.DropTable(
                name: "Applications");
        }
    }
}
