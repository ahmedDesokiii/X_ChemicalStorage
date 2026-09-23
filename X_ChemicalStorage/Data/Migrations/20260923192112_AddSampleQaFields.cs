using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleQaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalyticsResults",
                table: "Bio_Samples",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BacterialTestDate",
                table: "Bio_Samples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BacterialTestResult",
                table: "Bio_Samples",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "IsolateTestDate",
                table: "Bio_Samples",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IsolateTestResult",
                table: "Bio_Samples",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SOPId",
                table: "Bio_Samples",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Bio_SOPs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_SOPs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bio_SOPs");

            migrationBuilder.DropColumn(
                name: "AnalyticsResults",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "BacterialTestDate",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "BacterialTestResult",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "IsolateTestDate",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "IsolateTestResult",
                table: "Bio_Samples");

            migrationBuilder.DropColumn(
                name: "SOPId",
                table: "Bio_Samples");
        }
    }
}
