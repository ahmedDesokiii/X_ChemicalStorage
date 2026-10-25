using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class TempSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Zone",
                table: "StorageUnits",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Inventory");

            migrationBuilder.CreateTable(
                name: "TS_Samples",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SampleCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    QrImagePath = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExternalBarcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeliveryDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceptionDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DeliveringOrganization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceivedByUserId = table.Column<int>(type: "int", nullable: false),
                    ReceivedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ReceivingOrganization = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SampleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QuantityReceived = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CurrentQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    ContainersCount = table.Column<int>(type: "int", nullable: false),
                    RequestedAnalysis = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransportCondition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ArrivalTemperatureC = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    ArrivedOutOfRange = table.Column<bool>(type: "bit", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PlannedWithdrawalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BoxId = table.Column<int>(type: "int", nullable: true),
                    RowIndex = table.Column<int>(type: "int", nullable: true),
                    ColumnIndex = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TS_Samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TS_Samples_Boxes_BoxId",
                        column: x => x.BoxId,
                        principalTable: "Boxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TS_Samples_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TS_Attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TempSampleId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TS_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TS_Attachments_TS_Samples_TempSampleId",
                        column: x => x.TempSampleId,
                        principalTable: "TS_Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TS_CustodyEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TempSampleId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedByUserId = table.Column<int>(type: "int", nullable: true),
                    PerformedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Counterparty = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FromLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ToLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    QuantityDelta = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    QuantityAfter = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    StatusAfter = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientInfo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PreviousHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TS_CustodyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TS_CustodyEvents_TS_Samples_TempSampleId",
                        column: x => x.TempSampleId,
                        principalTable: "TS_Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TS_Attachments_TempSampleId",
                table: "TS_Attachments",
                column: "TempSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_TS_CustodyEvents_OccurredAtUtc",
                table: "TS_CustodyEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TS_CustodyEvents_TempSampleId_Sequence",
                table: "TS_CustodyEvents",
                columns: new[] { "TempSampleId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TS_Samples_BoxId_RowIndex_ColumnIndex",
                table: "TS_Samples",
                columns: new[] { "BoxId", "RowIndex", "ColumnIndex" },
                unique: true,
                filter: "[BoxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Samples_ExternalBarcode",
                table: "TS_Samples",
                column: "ExternalBarcode");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Samples_PlannedWithdrawalDate",
                table: "TS_Samples",
                column: "PlannedWithdrawalDate");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Samples_RequestReferenceNumber",
                table: "TS_Samples",
                column: "RequestReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Samples_SampleCode",
                table: "TS_Samples",
                column: "SampleCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TS_Samples_Status",
                table: "TS_Samples",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TS_Samples_UnitId",
                table: "TS_Samples",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TS_Attachments");

            migrationBuilder.DropTable(
                name: "TS_CustodyEvents");

            migrationBuilder.DropTable(
                name: "TS_Samples");

            migrationBuilder.DropColumn(
                name: "Zone",
                table: "StorageUnits");
        }
    }
}
