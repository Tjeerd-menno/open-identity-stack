using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OpenIdentityStackDbContext))]
    [Migration("20260524121000_BackfillServiceAccountsToApplications")]
    public partial class BackfillServiceAccountsToApplications : Migration
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
                        FROM "ServiceAccounts" s
                        WHERE NOT (
                            jsonb_array_length(s."AllowedGrantTypes") = 1
                            AND s."AllowedGrantTypes" ? 'client_credentials'
                        )
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate service accounts with grants other than client_credentials.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "Applications" (
                    "Id",
                    "ClientId",
                    "DisplayName",
                    "Description",
                    "Type",
                    "ClientType",
                    "Status",
                    "RequirePkce",
                    "RequireConsent",
                    "RequiresMigrationReview",
                    "MigrationSource",
                    "AllowedGrantTypes",
                    "AllowedScopes",
                    "PostLogoutRedirectUris",
                    "RedirectUris",
                    "CreatedAt",
                    "ModifiedAt")
                SELECT
                    gen_random_uuid(),
                    s."ClientId",
                    s."DisplayName",
                    NULL,
                    0,
                    1,
                    CASE WHEN s."Status" = 2 THEN 1 ELSE 0 END,
                    FALSE,
                    FALSE,
                    FALSE,
                    'ServiceAccount',
                    '["client_credentials"]'::jsonb,
                    s."AllowedScopes",
                    '[]'::jsonb,
                    '[]'::jsonb,
                    s."CreatedAt",
                    s."ModifiedAt"
                FROM "ServiceAccounts" s
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Applications" a
                    WHERE lower(a."ClientId") = lower(s."ClientId"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "Applications" WHERE "MigrationSource" = 'ServiceAccount';""");
        }
    }
}
