using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class TryLotDireignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LotTransactions_Lots_LotId",
                table: "LotTransactions");

            migrationBuilder.DropIndex(
                name: "IX_LotTransactions_LotId",
                table: "LotTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LotTransactions_LotId",
                table: "LotTransactions",
                column: "LotId");

            migrationBuilder.AddForeignKey(
                name: "FK_LotTransactions_Lots_LotId",
                table: "LotTransactions",
                column: "LotId",
                principalTable: "Lots",
                principalColumn: "Id");
        }
    }
}
