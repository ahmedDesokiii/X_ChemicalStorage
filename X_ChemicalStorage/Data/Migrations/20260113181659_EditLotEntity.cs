using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class EditLotEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BarcodeType",
                table: "Lots");

            migrationBuilder.DropColumn(
                name: "BarcodeValue",
                table: "Lots");

            migrationBuilder.AlterColumn<bool>(
                name: "SDS",
                table: "Lots",
                type: "bit",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Quantity",
                table: "Lots",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Lots");

            migrationBuilder.AlterColumn<string>(
                name: "SDS",
                table: "Lots",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BarcodeType",
                table: "Lots",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BarcodeValue",
                table: "Lots",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
