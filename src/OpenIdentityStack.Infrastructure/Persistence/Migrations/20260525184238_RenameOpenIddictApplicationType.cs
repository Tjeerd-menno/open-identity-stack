using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameOpenIddictApplicationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'OpenIddictApplications'
                          AND column_name = 'ApplicationProfile')
                    AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'OpenIddictApplications'
                          AND column_name = 'ApplicationType')
                    THEN
                        ALTER TABLE "OpenIddictApplications" RENAME COLUMN "ApplicationProfile" TO "ApplicationType";
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'OpenIddictApplications'
                          AND column_name = 'ApplicationType')
                    AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'OpenIddictApplications'
                          AND column_name = 'ApplicationProfile')
                    THEN
                        ALTER TABLE "OpenIddictApplications" RENAME COLUMN "ApplicationType" TO "ApplicationProfile";
                    END IF;
                END
                $$;
                """);
        }
    }
}
