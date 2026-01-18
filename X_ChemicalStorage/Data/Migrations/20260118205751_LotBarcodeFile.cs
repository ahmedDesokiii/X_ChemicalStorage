using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class LotBarcodeFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BarcodeImage",
                table: "Lots",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BarcodeImage",
                table: "Lots");
        }
    }
}
