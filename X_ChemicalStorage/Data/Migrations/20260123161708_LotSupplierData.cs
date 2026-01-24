using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class LotSupplierData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LotId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_LotId",
                table: "Suppliers",
                column: "LotId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Lots_LotId",
                table: "Suppliers",
                column: "LotId",
                principalTable: "Lots",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Lots_LotId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_LotId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "LotId",
                table: "Suppliers");
        }
    }
}
