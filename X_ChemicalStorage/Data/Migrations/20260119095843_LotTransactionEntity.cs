using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class LotTransactionEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ItemTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceUsing",
                table: "ItemTransactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LotTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Move_Num = table.Column<int>(type: "int", nullable: true),
                    Move_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Move_State = table.Column<bool>(type: "bit", nullable: true),
                    Move_Statement = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Move_Quantity = table.Column<double>(type: "float", nullable: true),
                    Total_Quantity = table.Column<double>(type: "float", nullable: true),
                    LotId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentState = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotTransactions_Lots_LotId",
                        column: x => x.LotId,
                        principalTable: "Lots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotTransactions_LotId",
                table: "LotTransactions",
                column: "LotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LotTransactions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ItemTransactions");

            migrationBuilder.DropColumn(
                name: "DeviceUsing",
                table: "ItemTransactions");
        }
    }
}
