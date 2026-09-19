using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class BioBankEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bio_Freezers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Temperature = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShelvesCount = table.Column<int>(type: "int", nullable: false),
                    RacksPerShelf = table.Column<int>(type: "int", nullable: false),
                    DrawersPerRack = table.Column<int>(type: "int", nullable: false),
                    BoxesPerDrawer = table.Column<int>(type: "int", nullable: false),
                    TubesPerBox = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_Freezers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bio_Shelves",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FreezerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ShelfIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_Shelves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bio_Shelves_Bio_Freezers_FreezerId",
                        column: x => x.FreezerId,
                        principalTable: "Bio_Freezers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bio_Racks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FreezerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ShelfId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ShelfIndex = table.Column<int>(type: "int", nullable: false),
                    RackIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_Racks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bio_Racks_Bio_Freezers_FreezerId",
                        column: x => x.FreezerId,
                        principalTable: "Bio_Freezers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bio_Racks_Bio_Shelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "Bio_Shelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bio_Drawers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RackId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DrawerIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_Drawers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bio_Drawers_Bio_Racks_RackId",
                        column: x => x.RackId,
                        principalTable: "Bio_Racks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bio_Boxes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DrawerId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    RackId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BoxIndex = table.Column<int>(type: "int", nullable: false),
                    Rows = table.Column<int>(type: "int", nullable: false),
                    Columns = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_Boxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bio_Boxes_Bio_Drawers_DrawerId",
                        column: x => x.DrawerId,
                        principalTable: "Bio_Drawers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bio_Boxes_Bio_Racks_RackId",
                        column: x => x.RackId,
                        principalTable: "Bio_Racks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bio_Tubes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    BoxId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Position = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Row = table.Column<int>(type: "int", nullable: false),
                    Column = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SampleId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SampleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StoredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bio_Tubes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bio_Tubes_Bio_Boxes_BoxId",
                        column: x => x.BoxId,
                        principalTable: "Bio_Boxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Boxes_DrawerId",
                table: "Bio_Boxes",
                column: "DrawerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Boxes_RackId",
                table: "Bio_Boxes",
                column: "RackId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Drawers_RackId",
                table: "Bio_Drawers",
                column: "RackId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Freezers_Barcode",
                table: "Bio_Freezers",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Racks_FreezerId",
                table: "Bio_Racks",
                column: "FreezerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Racks_ShelfId",
                table: "Bio_Racks",
                column: "ShelfId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Shelves_FreezerId",
                table: "Bio_Shelves",
                column: "FreezerId");

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Tubes_BoxId_Position",
                table: "Bio_Tubes",
                columns: new[] { "BoxId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bio_Tubes_SampleId",
                table: "Bio_Tubes",
                column: "SampleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bio_Tubes");

            migrationBuilder.DropTable(
                name: "Bio_Boxes");

            migrationBuilder.DropTable(
                name: "Bio_Drawers");

            migrationBuilder.DropTable(
                name: "Bio_Racks");

            migrationBuilder.DropTable(
                name: "Bio_Shelves");

            migrationBuilder.DropTable(
                name: "Bio_Freezers");
        }
    }
}
