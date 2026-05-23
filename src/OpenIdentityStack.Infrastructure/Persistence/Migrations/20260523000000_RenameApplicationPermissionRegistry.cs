using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OpenIdentityStackDbContext))]
    [Migration("20260523000000_RenameApplicationPermissionRegistry")]
    public partial class RenameApplicationPermissionRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "RegisteredServices",
                newName: "RegisteredApplications");

            migrationBuilder.RenameTable(
                name: "ServicePermissions",
                newName: "ApplicationPermissions");

            migrationBuilder.RenameColumn(
                name: "ServiceIdentifier",
                table: "RegisteredApplications",
                newName: "ApplicationIdentifier");

            migrationBuilder.RenameColumn(
                name: "RegisteredServiceId",
                table: "ApplicationPermissions",
                newName: "RegisteredApplicationId");

            migrationBuilder.RenameColumn(
                name: "RegisteredServiceId",
                table: "DelegatedMaintainers",
                newName: "RegisteredApplicationId");

            migrationBuilder.RenameColumn(
                name: "IntendedUse",
                table: "ApplicationPermissions",
                newName: "Category");

            migrationBuilder.DropColumn(
                name: "DocumentationUrl",
                table: "ApplicationPermissions");

            migrationBuilder.RenameIndex(
                name: "IX_RegisteredServices_OwnerId",
                table: "RegisteredApplications",
                newName: "IX_RegisteredApplications_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_RegisteredServices_ServiceIdentifier",
                table: "RegisteredApplications",
                newName: "IX_RegisteredApplications_ApplicationIdentifier");

            migrationBuilder.RenameIndex(
                name: "IX_RegisteredServices_Status",
                table: "RegisteredApplications",
                newName: "IX_RegisteredApplications_Status");

            migrationBuilder.RenameIndex(
                name: "IX_ServicePermissions_FullPermissionKey",
                table: "ApplicationPermissions",
                newName: "IX_ApplicationPermissions_FullPermissionKey");

            migrationBuilder.RenameIndex(
                name: "IX_ServicePermissions_ServiceId_PermissionKey",
                table: "ApplicationPermissions",
                newName: "IX_ApplicationPermissions_ApplicationId_PermissionKey");

            migrationBuilder.RenameIndex(
                name: "IX_DelegatedMaintainers_Service_Principal",
                table: "DelegatedMaintainers",
                newName: "IX_DelegatedMaintainers_Application_Principal");

            migrationBuilder.RenameIndex(
                name: "IX_ServicePermissions_Status",
                table: "ApplicationPermissions",
                newName: "IX_ApplicationPermissions_Status");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationPermissions_Status",
                table: "ApplicationPermissions");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationPermissions_IsAssignable",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "DeprecatedAt",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "DisabledAt",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "IsAssignable",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "ApplicationPermissions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ApplicationPermissions");

            migrationBuilder.Sql("""ALTER TABLE "RegisteredApplications" RENAME CONSTRAINT "PK_RegisteredServices" TO "PK_RegisteredApplications";""");
            migrationBuilder.Sql("""ALTER TABLE "ApplicationPermissions" RENAME CONSTRAINT "PK_ServicePermissions" TO "PK_ApplicationPermissions";""");
            migrationBuilder.Sql("""ALTER TABLE "DelegatedMaintainers" RENAME CONSTRAINT "FK_DelegatedMaintainers_RegisteredServices_RegisteredServiceId" TO "FK_DelegatedMaintainers_RegisteredApplications_RegisteredApplicationId";""");
            migrationBuilder.Sql("""ALTER TABLE "ApplicationPermissions" RENAME CONSTRAINT "FK_ServicePermissions_RegisteredServices_RegisteredServiceId" TO "FK_ApplicationPermissions_RegisteredApplications_RegisteredApplicationId";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "ApplicationPermissions" RENAME CONSTRAINT "FK_ApplicationPermissions_RegisteredApplications_RegisteredApplicationId" TO "FK_ServicePermissions_RegisteredServices_RegisteredServiceId";""");
            migrationBuilder.Sql("""ALTER TABLE "DelegatedMaintainers" RENAME CONSTRAINT "FK_DelegatedMaintainers_RegisteredApplications_RegisteredApplicationId" TO "FK_DelegatedMaintainers_RegisteredServices_RegisteredServiceId";""");
            migrationBuilder.Sql("""ALTER TABLE "ApplicationPermissions" RENAME CONSTRAINT "PK_ApplicationPermissions" TO "PK_ServicePermissions";""");
            migrationBuilder.Sql("""ALTER TABLE "RegisteredApplications" RENAME CONSTRAINT "PK_RegisteredApplications" TO "PK_RegisteredServices";""");

            migrationBuilder.RenameIndex(
                name: "IX_DelegatedMaintainers_Application_Principal",
                table: "DelegatedMaintainers",
                newName: "IX_DelegatedMaintainers_Service_Principal");

            migrationBuilder.RenameIndex(
                name: "IX_ApplicationPermissions_ApplicationId_PermissionKey",
                table: "ApplicationPermissions",
                newName: "IX_ServicePermissions_ServiceId_PermissionKey");

            migrationBuilder.RenameIndex(
                name: "IX_ApplicationPermissions_FullPermissionKey",
                table: "ApplicationPermissions",
                newName: "IX_ServicePermissions_FullPermissionKey");

            migrationBuilder.RenameIndex(
                name: "IX_RegisteredApplications_Status",
                table: "RegisteredApplications",
                newName: "IX_RegisteredServices_Status");

            migrationBuilder.RenameIndex(
                name: "IX_RegisteredApplications_ApplicationIdentifier",
                table: "RegisteredApplications",
                newName: "IX_RegisteredServices_ServiceIdentifier");

            migrationBuilder.RenameIndex(
                name: "IX_RegisteredApplications_OwnerId",
                table: "RegisteredApplications",
                newName: "IX_RegisteredServices_OwnerId");

            migrationBuilder.RenameColumn(
                name: "RegisteredApplicationId",
                table: "DelegatedMaintainers",
                newName: "RegisteredServiceId");

            migrationBuilder.RenameColumn(
                name: "RegisteredApplicationId",
                table: "ApplicationPermissions",
                newName: "RegisteredServiceId");

            migrationBuilder.AddColumn<string>(
                name: "DocumentationUrl",
                table: "ApplicationPermissions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeprecatedAt",
                table: "ApplicationPermissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAt",
                table: "ApplicationPermissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAssignable",
                table: "ApplicationPermissions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetiredAt",
                table: "ApplicationPermissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ApplicationPermissions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "ApplicationPermissions",
                newName: "IntendedUse");

            migrationBuilder.RenameColumn(
                name: "ApplicationIdentifier",
                table: "RegisteredApplications",
                newName: "ServiceIdentifier");

            migrationBuilder.RenameTable(
                name: "ApplicationPermissions",
                newName: "ServicePermissions");

            migrationBuilder.RenameTable(
                name: "RegisteredApplications",
                newName: "RegisteredServices");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePermissions_IsAssignable",
                table: "ServicePermissions",
                column: "IsAssignable");

            migrationBuilder.CreateIndex(
                name: "IX_ServicePermissions_Status",
                table: "ServicePermissions",
                column: "Status");
        }
    }
}
