using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBioSamplesPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bio_Samples",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SampleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PatientRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProjectCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CurrentTubeId = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    CurrentBoxId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CurrentPosition = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    CurrentFreezerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_Samples", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bio_SampleMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SampleId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromTubeId = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    ToTubeId = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    FromPosition = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    ToPosition = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    FromBoxId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ToBoxId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_SampleMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bio_SampleMovements_Bio_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Bio_Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bio_SampleMovements_PerformedAt",
                table: "Bio_SampleMovements",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_SampleMovements_SampleId",
                table: "Bio_SampleMovements",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Samples_CurrentTubeId",
                table: "Bio_Samples",
                column: "CurrentTubeId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Samples_ExternalId",
                table: "Bio_Samples",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Samples_PatientRef",
                table: "Bio_Samples",
                column: "PatientRef");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Samples_ProjectCode",
                table: "Bio_Samples",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Samples_Status",
                table: "Bio_Samples",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bio_SampleMovements");

            migrationBuilder.DropTable(
                name: "Bio_Samples");
        }
    }
}
