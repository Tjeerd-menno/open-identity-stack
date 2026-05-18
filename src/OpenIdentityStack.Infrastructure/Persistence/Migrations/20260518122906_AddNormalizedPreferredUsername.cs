using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedPreferredUsername : Migration
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
                        FROM "Users"
                        WHERE "PreferredUsername" IS NOT NULL
                        GROUP BY UPPER("PreferredUsername")
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot add a case-insensitive preferred username index because existing preferred usernames collide after normalization.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Users_PreferredUsername",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPreferredUsername",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "NormalizedPreferredUsername" = UPPER("PreferredUsername")
                WHERE "PreferredUsername" IS NOT NULL
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedPreferredUsername",
                table: "Users",
                column: "NormalizedPreferredUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedPreferredUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedPreferredUsername",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PreferredUsername",
                table: "Users",
                column: "PreferredUsername",
                unique: true);
        }
    }
}
