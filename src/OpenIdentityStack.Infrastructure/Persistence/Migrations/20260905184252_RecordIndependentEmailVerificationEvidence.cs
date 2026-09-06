using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordIndependentEmailVerificationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserEmailVerificationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEmailVerificationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserEmailVerificationEvidence_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserEmailVerificationEvidence_UserId",
                table: "UserEmailVerificationEvidence",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEmailVerificationEvidence");
        }
    }
}
