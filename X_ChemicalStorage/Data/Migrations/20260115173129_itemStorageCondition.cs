using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class itemStorageCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Total_Quantity",
                table: "ItemTransactions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageCondition",
                table: "Items",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Total_Quantity",
                table: "ItemTransactions");

            migrationBuilder.DropColumn(
                name: "StorageCondition",
                table: "Items");
        }
    }
}
