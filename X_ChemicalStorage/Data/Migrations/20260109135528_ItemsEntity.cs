using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class ItemsEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lots_Substances_SubstanceId",
                table: "Lots");

            migrationBuilder.DropTable(
                name: "Substances");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Lots");

            migrationBuilder.RenameColumn(
                name: "SubstanceId",
                table: "Lots",
                newName: "ItemId");

            migrationBuilder.RenameIndex(
                name: "IX_Lots_SubstanceId",
                table: "Lots",
                newName: "IX_Lots_ItemId");

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SDS = table.Column<bool>(type: "bit", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    CurrentState = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Items_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Items_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_CategoryId",
                table: "Items",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_LocationId",
                table: "Items",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_UnitId",
                table: "Items",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lots_Items_ItemId",
                table: "Lots",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lots_Items_ItemId",
                table: "Lots");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.RenameColumn(
                name: "ItemId",
                table: "Lots",
                newName: "SubstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_Lots_ItemId",
                table: "Lots",
                newName: "IX_Lots_SubstanceId");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Lots",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Substances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    ManufacturerId = table.Column<int>(type: "int", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    CAS_Number = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Chemical_Formula = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Concentration = table.Column<double>(type: "float", nullable: true),
                    CurrentState = table.Column<int>(type: "int", nullable: true),
                    GHS_Classification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hazard_Statements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IUPAC_Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Physical_State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Size = table.Column<double>(type: "float", nullable: true),
                    Substance_Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Synonym = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_Substances_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id");
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_Substances_UnitId",
                table: "Substances",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lots_Substances_SubstanceId",
                table: "Lots",
                column: "SubstanceId",
                principalTable: "Substances",
                principalColumn: "Id");
        }
    }
}
