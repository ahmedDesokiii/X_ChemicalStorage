using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBloodSampleAndCheckOutFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Bio_Tubes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BloodType",
                table: "Bio_Samples",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CheckOutReason",
                table: "Bio_Samples",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedOutAt",
                table: "Bio_Samples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckedOutBy",
                table: "Bio_Samples",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectedByEmployeeId",
                table: "Bio_Samples",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedReturnAt",
                table: "Bio_Samples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FastaAccessCode",
                table: "Bio_Samples",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingVolumeMl",
                table: "Bio_Samples",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                table: "Bio_Samples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VolumeMl",
                table: "Bio_Samples",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Tubes_Barcode",
                table: "Bio_Tubes",
                column: "Barcode",
                unique: true,
                filter: "[Barcode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bio_Tubes_Barcode",
                table: "Bio_Tubes");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Bio_Tubes");

            migrationBuilder.DropColumn(
                name: "BloodType",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "CheckOutReason",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "CheckedOutAt",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "CheckedOutBy",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "CollectedByEmployeeId",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "ExpectedReturnAt",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "FastaAccessCode",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "RemainingVolumeMl",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "VolumeMl",
                table: "Bio_Samples");
        }
    }
}
