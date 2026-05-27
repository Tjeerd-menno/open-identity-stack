using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OpenIdentityStackDbContext))]
    [Migration("20260524123000_MigrateApplicationRolePermissions")]
    public partial class MigrateApplicationRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Roles" r
                SET "Permissions" = COALESCE((
                    SELECT jsonb_agg(mapped."Permission" ORDER BY mapped."FirstOrdinal")
                    FROM (
                        SELECT mapped_values."Permission", MIN(mapped_values."Ordinality") AS "FirstOrdinal"
                        FROM (
                            SELECT
                                CASE permission_value
                                    WHEN 'clients:read' THEN 'applications:read'
                                    WHEN 'clients:write' THEN 'applications:write'
                                    WHEN 'clients:delete' THEN 'applications:delete'
                                    WHEN 'clients:manage-secrets' THEN 'applications:manage-credentials'
                                    WHEN 'clients:*' THEN 'applications:*'
                                    WHEN 'service-accounts:read' THEN 'applications:read'
                                    WHEN 'service-accounts:write' THEN 'applications:write'
                                    WHEN 'service-accounts:delete' THEN 'applications:delete'
                                    WHEN 'service-accounts:rotate-secret' THEN 'applications:manage-credentials'
                                    WHEN 'service-accounts:manage-certificates' THEN 'applications:manage-certificates'
                                    WHEN 'service-accounts:*' THEN 'applications:*'
                                    ELSE permission_value
                                END AS "Permission",
                                ordinality AS "Ordinality"
                            FROM jsonb_array_elements_text(r."Permissions") WITH ORDINALITY AS permissions(permission_value, ordinality)
                        ) mapped_values
                        GROUP BY mapped_values."Permission"
                    ) mapped
                ), '[]'::jsonb)
                WHERE r."Permissions" ?| ARRAY[
                    'clients:read',
                    'clients:write',
                    'clients:delete',
                    'clients:manage-secrets',
                    'clients:*',
                    'service-accounts:read',
                    'service-accounts:write',
                    'service-accounts:delete',
                    'service-accounts:rotate-secret',
                    'service-accounts:manage-certificates',
                    'service-accounts:*'
                ];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
