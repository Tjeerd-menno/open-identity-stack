using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistCredentialCutoverBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CredentialBoundary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Epoch = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredentialBoundary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CredentialCutovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Tokens = table.Column<long>(type: "bigint", nullable: false),
                    Grants = table.Column<long>(type: "bigint", nullable: false),
                    Sessions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredentialCutovers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CredentialBoundary",
                columns: new[] { "Id", "Epoch" },
                values: new object[] { 1, new Guid("00000000-0000-0000-0000-000000000000") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CredentialBoundary");

            migrationBuilder.DropTable(
                name: "CredentialCutovers");
        }
    }
}
