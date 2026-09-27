using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFreezerIoT : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentTemperature",
                table: "Bio_Freezers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReadingAt",
                table: "Bio_Freezers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Bio_FreezerAlarms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FreezerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AlarmType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsCleared = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_FreezerAlarms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bio_FreezerAlarms_Bio_Freezers_FreezerId",
                        column: x => x.FreezerId,
                        principalTable: "Bio_Freezers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bio_FreezerReadings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FreezerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DoorStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PowerStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_FreezerReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bio_FreezerReadings_Bio_Freezers_FreezerId",
                        column: x => x.FreezerId,
                        principalTable: "Bio_Freezers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bio_IoTDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FreezerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GatewaySerial = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CollectorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_IoTDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bio_IoTDevices_Bio_Freezers_FreezerId",
                        column: x => x.FreezerId,
                        principalTable: "Bio_Freezers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bio_FreezerAlarms_FreezerId",
                table: "Bio_FreezerAlarms",
                column: "FreezerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_FreezerReadings_FreezerId",
                table: "Bio_FreezerReadings",
                column: "FreezerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_IoTDevices_FreezerId",
                table: "Bio_IoTDevices",
                column: "FreezerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bio_FreezerAlarms");

            migrationBuilder.DropTable(
                name: "Bio_FreezerReadings");

            migrationBuilder.DropTable(
                name: "Bio_IoTDevices");

            migrationBuilder.DropColumn(
                name: "CurrentTemperature",
                table: "Bio_Freezers");

            migrationBuilder.DropColumn(
                name: "LastReadingAt",
                table: "Bio_Freezers");
        }
    }
}
