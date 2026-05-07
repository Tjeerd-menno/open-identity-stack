using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePermissionRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegisteredServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceIdentifier = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DelegatedMaintainers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrincipalId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PrincipalType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GrantedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegatedMaintainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelegatedMaintainers_RegisteredServices_RegisteredServiceId",
                        column: x => x.RegisteredServiceId,
                        principalTable: "RegisteredServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServicePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionKey = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    FullPermissionKey = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IntendedUse = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DocumentationUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsAssignable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServicePermissions_RegisteredServices_RegisteredServiceId",
                        column: x => x.RegisteredServiceId,
                        principalTable: "RegisteredServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegatedMaintainers_Service_Principal",
                table: "DelegatedMaintainers",
                columns: new[] { "RegisteredServiceId", "PrincipalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredServices_OwnerId",
                table: "RegisteredServices",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredServices_ServiceIdentifier",
                table: "RegisteredServices",
                column: "ServiceIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredServices_Status",
                table: "RegisteredServices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePermissions_FullPermissionKey",
                table: "ServicePermissions",
                column: "FullPermissionKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServicePermissions_IsAssignable",
                table: "ServicePermissions",
                column: "IsAssignable");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePermissions_ServiceId_PermissionKey",
                table: "ServicePermissions",
                columns: new[] { "RegisteredServiceId", "PermissionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegatedMaintainers");

            migrationBuilder.DropTable(
                name: "ServicePermissions");

            migrationBuilder.DropTable(
                name: "RegisteredServices");
        }
    }
}
