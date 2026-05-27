using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OpenIdentityStackDbContext))]
    [Migration("20260524122000_BackfillServiceAccountCredentialsToApplicationCredentials")]
    public partial class BackfillServiceAccountCredentialsToApplicationCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "ApplicationCredentials" (
                    "Id",
                    "ApplicationId",
                    "Type",
                    "SecretHash",
                    "Thumbprint",
                    "Subject",
                    "Description",
                    "ExpiresAt",
                    "CreatedAt",
                    "LastUsedAt",
                    "RevokedAt",
                    "ModifiedAt")
                SELECT
                    c."Id",
                    a."Id",
                    0,
                    c."SecretHash",
                    NULL,
                    NULL,
                    c."Description",
                    c."ExpiresAt",
                    c."CreatedAt",
                    c."LastUsedAt",
                    CASE WHEN c."IsRevoked" THEN CURRENT_TIMESTAMP ELSE NULL END,
                    c."CreatedAt"
                FROM "ClientCredentials" c
                INNER JOIN "ServiceAccounts" s ON s."Id" = c."ServiceAccountId"
                INNER JOIN "Applications" a ON lower(a."ClientId") = lower(s."ClientId")
                WHERE a."MigrationSource" = 'ServiceAccount'
                  AND NOT EXISTS (
                      SELECT 1 FROM "ApplicationCredentials" existing
                      WHERE existing."Id" = c."Id");
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "ApplicationCredentials" (
                    "Id",
                    "ApplicationId",
                    "Type",
                    "SecretHash",
                    "Thumbprint",
                    "Subject",
                    "Description",
                    "ExpiresAt",
                    "CreatedAt",
                    "LastUsedAt",
                    "RevokedAt",
                    "ModifiedAt")
                SELECT
                    c."Id",
                    a."Id",
                    1,
                    NULL,
                    c."Thumbprint",
                    c."Subject",
                    c."Subject",
                    c."ExpiresAt",
                    c."CreatedAt",
                    NULL,
                    CASE WHEN c."IsRevoked" THEN CURRENT_TIMESTAMP ELSE NULL END,
                    c."CreatedAt"
                FROM "ClientCertificates" c
                INNER JOIN "ServiceAccounts" s ON s."Id" = c."ServiceAccountId"
                INNER JOIN "Applications" a ON lower(a."ClientId") = lower(s."ClientId")
                WHERE a."MigrationSource" = 'ServiceAccount'
                  AND NOT EXISTS (
                      SELECT 1 FROM "ApplicationCredentials" existing
                      WHERE existing."Id" = c."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "ApplicationCredentials" c
                USING "Applications" a
                WHERE c."ApplicationId" = a."Id"
                  AND a."MigrationSource" = 'ServiceAccount';
                """);
        }
    }
}
