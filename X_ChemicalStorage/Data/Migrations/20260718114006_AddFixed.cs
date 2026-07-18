using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Locations_LocationId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Lots_Items_ItemId",
                table: "Lots");

            migrationBuilder.DropForeignKey(
                name: "FK_Lots_Locations_LocationId",
                table: "Lots");

            migrationBuilder.DropForeignKey(
                name: "FK_LotTransactions_Lots_LotId",
                table: "LotTransactions");

            migrationBuilder.DropIndex(
                name: "IX_LotTransactions_LotId",
                table: "LotTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Lots_LocationId",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "StorageCondition",
                table: "Items");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "SupplierLots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BoxId",
                table: "Lots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ColumnIndex",
                table: "Lots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RowIndex",
                table: "Lots",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "LocationId",
                table: "Items",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "BoxId",
                table: "Items",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ColumnIndex",
                table: "Items",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RowIndex",
                table: "Items",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StorageUnitTypeId",
                table: "Items",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageUnitTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemperatureValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageUnitTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    StorageUnitTypeId = table.Column<int>(type: "int", nullable: false),
                    IsHazardous = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageUnits_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageUnits_StorageUnitTypes_StorageUnitTypeId",
                        column: x => x.StorageUnitTypeId,
                        principalTable: "StorageUnitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shelves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StorageUnitId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shelves_StorageUnits_StorageUnitId",
                        column: x => x.StorageUnitId,
                        principalTable: "StorageUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Boxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowsCount = table.Column<int>(type: "int", nullable: false),
                    ColumnsCount = table.Column<int>(type: "int", nullable: false),
                    ShelfId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Boxes_Shelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "Shelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "StorageUnitTypes",
                columns: new[] { "Id", "Name", "TemperatureValue" },
                values: new object[,]
                {
                    { 1, "Freezer-20°C", -20m },
                    { 2, "Freezer-80°C", -80m },
                    { 3, "Freezer-150°C", -150m },
                    { 4, "2°C-8°C", 4m },
                    { 5, "Liquid Nitrogen Tank", null },
                    { 6, "Room Temp", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Lots_BoxId_RowIndex_ColumnIndex",
                table: "Lots",
                columns: new[] { "BoxId", "RowIndex", "ColumnIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_BoxId",
                table: "Items",
                column: "BoxId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_StorageUnitTypeId",
                table: "Items",
                column: "StorageUnitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Boxes_ShelfId",
                table: "Boxes",
                column: "ShelfId");

            migrationBuilder.CreateIndex(
                name: "IX_Shelves_StorageUnitId",
                table: "Shelves",
                column: "StorageUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageUnits_RoomId",
                table: "StorageUnits",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageUnits_StorageUnitTypeId",
                table: "StorageUnits",
                column: "StorageUnitTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Boxes_BoxId",
                table: "Items",
                column: "BoxId",
                principalTable: "Boxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Locations_LocationId",
                table: "Items",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_StorageUnitTypes_StorageUnitTypeId",
                table: "Items",
                column: "StorageUnitTypeId",
                principalTable: "StorageUnitTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lots_Boxes_BoxId",
                table: "Lots",
                column: "BoxId",
                principalTable: "Boxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Lots_Items_ItemId",
                table: "Lots",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Boxes_BoxId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Locations_LocationId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_StorageUnitTypes_StorageUnitTypeId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Lots_Boxes_BoxId",
                table: "Lots");

            migrationBuilder.DropForeignKey(
                name: "FK_Lots_Items_ItemId",
                table: "Lots");

            migrationBuilder.DropTable(
                name: "Boxes");

            migrationBuilder.DropTable(
                name: "Shelves");

            migrationBuilder.DropTable(
                name: "StorageUnits");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "StorageUnitTypes");

            migrationBuilder.DropIndex(
                name: "IX_Lots_BoxId_RowIndex_ColumnIndex",
                table: "Lots");

            migrationBuilder.DropIndex(
                name: "IX_Items_BoxId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_StorageUnitTypeId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "SupplierLots");

            migrationBuilder.DropColumn(
                name: "BoxId",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "ColumnIndex",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "RowIndex",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "BoxId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ColumnIndex",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RowIndex",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "StorageUnitTypeId",
                table: "Items");

            migrationBuilder.AddColumn<int>(
                name: "LocationId",
                table: "Lots",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "LocationId",
                table: "Items",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageCondition",
                table: "Items",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_LotTransactions_LotId",
                table: "LotTransactions",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_LocationId",
                table: "Lots",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Locations_LocationId",
                table: "Items",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Lots_Items_ItemId",
                table: "Lots",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Lots_Locations_LocationId",
                table: "Lots",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LotTransactions_Lots_LotId",
                table: "LotTransactions",
                column: "LotId",
                principalTable: "Lots",
                principalColumn: "Id");
        }
    }
}
