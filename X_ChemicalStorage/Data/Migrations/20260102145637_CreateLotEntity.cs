using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateLotEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Substances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Substance_Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IUPAC_Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CAS_Number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Synonym = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Chemical_Formula = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Physical_State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GHS_Classification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hazard_Statements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Size = table.Column<double>(type: "float", nullable: true),
                    Concentration = table.Column<double>(type: "float", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ManufacturerId = table.Column<int>(type: "int", nullable: true),
                    CurrentState = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Substances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Substances_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Substances_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Substances_ManufacuterCompanies_ManufacturerId",
                        column: x => x.ManufacturerId,
                        principalTable: "ManufacuterCompanies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Substances_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LotNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalQuantity = table.Column<double>(type: "float", nullable: true),
                    AvilableQuantity = table.Column<double>(type: "float", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ManufactureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BarcodeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BarcodeValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SDS_link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    SubstanceId = table.Column<int>(type: "int", nullable: true),
                    CurrentState = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lots_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Lots_Substances_SubstanceId",
                        column: x => x.SubstanceId,
                        principalTable: "Substances",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lots_LocationId",
                table: "Lots",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_SubstanceId",
                table: "Lots",
                column: "SubstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Substances_CategoryId",
                table: "Substances",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Substances_LocationId",
                table: "Substances",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Substances_ManufacturerId",
                table: "Substances",
                column: "ManufacturerId");

            migrationBuilder.CreateIndex(
                name: "IX_Substances_SupplierId",
                table: "Substances",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Lots");

            migrationBuilder.DropTable(
                name: "Substances");
        }
    }
}
