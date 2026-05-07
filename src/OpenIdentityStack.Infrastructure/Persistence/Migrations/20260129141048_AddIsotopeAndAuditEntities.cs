using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenIdentityStack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsotopeAndAuditEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    BeforeState = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    AfterState = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    Metadata = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsotopeExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CriteriaJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    XmlContent = table.Column<byte[]>(type: "bytea", nullable: true),
                    PreparedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PreparedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    SignatureMeaning = table.Column<int>(type: "integer", nullable: true),
                    SignatureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SignatureThumbprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsotopeExports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IsotopeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsotopeType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Scale = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DoseCalibratorModel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MeasurementTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BatchLot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SupplierSource = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsotopeRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportedRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExportId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportedRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportedRecords_IsotopeExports_ExportId",
                        column: x => x.ExportId,
                        principalTable: "IsotopeExports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IsotopeVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    IsotopeType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Scale = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DoseCalibratorModel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MeasurementTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BatchLot = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SupplierSource = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsotopeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsotopeVersions_IsotopeRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "IsotopeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Action",
                table: "AuditEntries",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ActorId",
                table: "AuditEntries",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_CorrelationId",
                table: "AuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityId",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_Timestamp",
                table: "AuditEntries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedRecords_ExportId_RecordId_VersionNumber",
                table: "ExportedRecords",
                columns: new[] { "ExportId", "RecordId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportedRecords_RecordId",
                table: "ExportedRecords",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportedRecords_RecordId_VersionNumber",
                table: "ExportedRecords",
                columns: new[] { "RecordId", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_IsotopeExports_CreatedAt",
                table: "IsotopeExports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IsotopeExports_PreparedBy",
                table: "IsotopeExports",
                column: "PreparedBy");

            migrationBuilder.CreateIndex(
                name: "IX_IsotopeExports_Status",
                table: "IsotopeExports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IsotopeRecords_IsotopeType",
                table: "IsotopeRecords",
                column: "IsotopeType",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_IsotopeRecords_MeasurementTimestamp",
                table: "IsotopeRecords",
                column: "MeasurementTimestamp",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_IsotopeRecords_Status",
                table: "IsotopeRecords",
                column: "Status",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_IsotopeVersions_RecordId",
                table: "IsotopeVersions",
                column: "RecordId");

            migrationBuilder.CreateIndex(
                name: "IX_IsotopeVersions_RecordId_VersionNumber",
                table: "IsotopeVersions",
                columns: new[] { "RecordId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "ExportedRecords");

            migrationBuilder.DropTable(
                name: "IsotopeVersions");

            migrationBuilder.DropTable(
                name: "IsotopeExports");

            migrationBuilder.DropTable(
                name: "IsotopeRecords");
        }
    }
}
