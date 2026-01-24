using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecipientLot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "LotTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ExchageQuantity",
                table: "Lots",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "Lots",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "LotTransactions");

            migrationBuilder.DropColumn(
                name: "ExchageQuantity",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "Lots");
        }
    }
}
