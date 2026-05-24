using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OpenIdentityStackDbContext))]
    [Migration("20260524120000_BackfillClientsToApplications")]
    public partial class BackfillClientsToApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    c."ClientIdValue",
                    c."DisplayName",
                    c."Description",
                    CASE
                        WHEN c."ClientType" = 0 AND c."AllowedGrantTypes" = '["client_credentials"]'::jsonb THEN 0
                        WHEN c."ClientType" = 0 AND c."AllowedGrantTypes" ? 'authorization_code' THEN 1
                        WHEN c."ClientType" = 1 AND c."AllowedGrantTypes" ? 'authorization_code' THEN 2
                        WHEN c."AllowedGrantTypes" ? 'urn:ietf:params:oauth:grant-type:device_code' THEN 4
                        ELSE 5
                    END,
                    CASE WHEN c."ClientType" = 0 THEN 1 ELSE 0 END,
                    0,
                    c."RequirePkce",
                    c."RequireConsent",
                    CASE
                        WHEN c."ClientType" = 1 AND c."AllowedGrantTypes" ? 'authorization_code' THEN TRUE
                        WHEN c."AllowedGrantTypes" ? 'implicit' OR c."AllowedGrantTypes" ? 'password' THEN TRUE
                        WHEN NOT (
                            (c."ClientType" = 0 AND c."AllowedGrantTypes" = '["client_credentials"]'::jsonb) OR
                            (c."ClientType" = 0 AND c."AllowedGrantTypes" ? 'authorization_code') OR
                            (c."AllowedGrantTypes" ? 'urn:ietf:params:oauth:grant-type:device_code')
                        ) THEN TRUE
                        ELSE FALSE
                    END,
                    'Client',
                    c."AllowedGrantTypes",
                    c."AllowedScopes",
                    c."PostLogoutRedirectUris",
                    c."RedirectUris",
                    c."CreatedAt",
                    c."ModifiedAt"
                FROM "Clients" c
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Applications" a
                    WHERE lower(a."ClientId") = lower(c."ClientIdValue"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "Applications" WHERE "MigrationSource" = 'Client';""");
        }
    }
}
