using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GateIdentityBoundaryCutover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmergencyAccessEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Epoch = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthenticatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyAccessEvidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceTokenWindowReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Epoch = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceRevision = table.Column<long>(type: "bigint", nullable: false),
                    Mechanism = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResidualSeconds = table.Column<int>(type: "integer", nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceTokenWindowReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyAccessEvidence_Epoch_RecordedAt",
                table: "EmergencyAccessEvidence",
                columns: new[] { "Epoch", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTokenWindowReviews_ResourceId_Epoch_ReviewedAt",
                table: "ResourceTokenWindowReviews",
                columns: new[] { "ResourceId", "Epoch", "ReviewedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmergencyAccessEvidence");

            migrationBuilder.DropTable(
                name: "ResourceTokenWindowReviews");
        }
    }
}
