using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class IoT_Step1_Settings_And_Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bio_FreezerAlarms_Bio_Freezers_FreezerId",
                table: "Bio_FreezerAlarms");

            migrationBuilder.DropForeignKey(
                name: "FK_Bio_FreezerReadings_Bio_Freezers_FreezerId",
                table: "Bio_FreezerReadings");

            migrationBuilder.DropIndex(
                name: "IX_Bio_FreezerReadings_FreezerId",
                table: "Bio_FreezerReadings");

            migrationBuilder.DropIndex(
                name: "IX_Bio_FreezerAlarms_FreezerId",
                table: "Bio_FreezerAlarms");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentTemperature",
                table: "Bio_Freezers",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Temperature",
                table: "Bio_FreezerReadings",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<decimal>(
                name: "AmbientTemperature",
                table: "Bio_FreezerReadings",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CryoCompressorRunning",
                table: "Bio_FreezerReadings",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HighCompressorRunning",
                table: "Bio_FreezerReadings",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SetTemperature",
                table: "Bio_FreezerReadings",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AckNote",
                table: "Bio_FreezerAlarms",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClearedAt",
                table: "Bio_FreezerAlarms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAt",
                table: "Bio_FreezerAlarms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PeakValue",
                table: "Bio_FreezerAlarms",
                type: "decimal(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Bio_FreezerSettings",
                columns: table => new
                {
                    FreezerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HighTempLimit = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    LowTempLimit = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    TempAlarmDelayMinutes = table.Column<int>(type: "int", nullable: false),
                    DoorOpenMinutes = table.Column<int>(type: "int", nullable: false),
                    OfflineAfterMinutes = table.Column<int>(type: "int", nullable: false),
                    EscalationMinutes = table.Column<int>(type: "int", nullable: false),
                    IsCommissioning = table.Column<bool>(type: "bit", nullable: false),
                    CommissioningEndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_FreezerSettings", x => x.FreezerId);
                    table.ForeignKey(
                        name: "FK_Bio_FreezerSettings_Bio_Freezers_FreezerId",
                        column: x => x.FreezerId,
                        principalTable: "Bio_Freezers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bio_IoTDevices_CollectorCode",
                table: "Bio_IoTDevices",
                column: "CollectorCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bio_FreezerReadings_FreezerId_Timestamp",
                table: "Bio_FreezerReadings",
                columns: new[] { "FreezerId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Bio_FreezerAlarms_FreezerId_IsCleared",
                table: "Bio_FreezerAlarms",
                columns: new[] { "FreezerId", "IsCleared" });

            migrationBuilder.AddForeignKey(
                name: "FK_Bio_FreezerAlarms_Bio_Freezers_FreezerId",
                table: "Bio_FreezerAlarms",
                column: "FreezerId",
                principalTable: "Bio_Freezers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bio_FreezerReadings_Bio_Freezers_FreezerId",
                table: "Bio_FreezerReadings",
                column: "FreezerId",
                principalTable: "Bio_Freezers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bio_FreezerAlarms_Bio_Freezers_FreezerId",
                table: "Bio_FreezerAlarms");

            migrationBuilder.DropForeignKey(
                name: "FK_Bio_FreezerReadings_Bio_Freezers_FreezerId",
                table: "Bio_FreezerReadings");

            migrationBuilder.DropTable(
                name: "Bio_FreezerSettings");

            migrationBuilder.DropIndex(
                name: "IX_Bio_IoTDevices_CollectorCode",
                table: "Bio_IoTDevices");

            migrationBuilder.DropIndex(
                name: "IX_Bio_FreezerReadings_FreezerId_Timestamp",
                table: "Bio_FreezerReadings");

            migrationBuilder.DropIndex(
                name: "IX_Bio_FreezerAlarms_FreezerId_IsCleared",
                table: "Bio_FreezerAlarms");

            migrationBuilder.DropColumn(
                name: "AmbientTemperature",
                table: "Bio_FreezerReadings");

            migrationBuilder.DropColumn(
                name: "CryoCompressorRunning",
                table: "Bio_FreezerReadings");

            migrationBuilder.DropColumn(
                name: "HighCompressorRunning",
                table: "Bio_FreezerReadings");

            migrationBuilder.DropColumn(
                name: "SetTemperature",
                table: "Bio_FreezerReadings");

            migrationBuilder.DropColumn(
                name: "AckNote",
                table: "Bio_FreezerAlarms");

            migrationBuilder.DropColumn(
                name: "ClearedAt",
                table: "Bio_FreezerAlarms");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "Bio_FreezerAlarms");

            migrationBuilder.DropColumn(
                name: "PeakValue",
                table: "Bio_FreezerAlarms");

            migrationBuilder.AlterColumn<decimal>(
                name: "CurrentTemperature",
                table: "Bio_Freezers",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(6,2)",
                oldPrecision: 6,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Temperature",
                table: "Bio_FreezerReadings",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(6,2)",
                oldPrecision: 6,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Bio_FreezerReadings_FreezerId",
                table: "Bio_FreezerReadings",
                column: "FreezerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_FreezerAlarms_FreezerId",
                table: "Bio_FreezerAlarms",
                column: "FreezerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bio_FreezerAlarms_Bio_Freezers_FreezerId",
                table: "Bio_FreezerAlarms",
                column: "FreezerId",
                principalTable: "Bio_Freezers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bio_FreezerReadings_Bio_Freezers_FreezerId",
                table: "Bio_FreezerReadings",
                column: "FreezerId",
                principalTable: "Bio_Freezers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
