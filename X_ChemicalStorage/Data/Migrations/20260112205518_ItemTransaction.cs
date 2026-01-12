using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class ItemTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SDS_link",
                table: "Lots",
                newName: "SDS");

            migrationBuilder.AlterColumn<int>(
                name: "LotNumber",
                table: "Lots",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AvilableQuantity",
                table: "Items",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TotalQuantity",
                table: "Items",
                type: "float",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Move_Num = table.Column<int>(type: "int", nullable: true),
                    Move_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Move_State = table.Column<bool>(type: "bit", nullable: true),
                    Move_Statement = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemId = table.Column<int>(type: "int", nullable: true),
                    CurrentState = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemTransactions_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SupplierLots",
                columns: table => new
                {
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierLots", x => new { x.SupplierId, x.LotId });
                    table.ForeignKey(
                        name: "FK_SupplierLots_Lots_LotId",
                        column: x => x.LotId,
                        principalTable: "Lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierLots_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemTransactions_ItemId",
                table: "ItemTransactions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierLots_LotId",
                table: "SupplierLots",
                column: "LotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemTransactions");

            migrationBuilder.DropTable(
                name: "SupplierLots");

            migrationBuilder.DropColumn(
                name: "AvilableQuantity",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "TotalQuantity",
                table: "Items");

            migrationBuilder.RenameColumn(
                name: "SDS",
                table: "Lots",
                newName: "SDS_link");

            migrationBuilder.AlterColumn<string>(
                name: "LotNumber",
                table: "Lots",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
