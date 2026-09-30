using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlarmNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlarmContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TelegramChatId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    FreezerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmContacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlarmNotifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlarmId = table.Column<long>(type: "bigint", nullable: false),
                    FreezerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContactId = table.Column<int>(type: "int", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmContacts_IsActive_Tier",
                table: "AlarmContacts",
                columns: new[] { "IsActive", "Tier" });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmNotifications_AlarmId",
                table: "AlarmNotifications",
                column: "AlarmId");

            migrationBuilder.CreateIndex(
                name: "IX_AlarmNotifications_FreezerId_CreatedAt",
                table: "AlarmNotifications",
                columns: new[] { "FreezerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmNotifications_Status_NextAttemptAt",
                table: "AlarmNotifications",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlarmContacts");

            migrationBuilder.DropTable(
                name: "AlarmNotifications");
        }
    }
}
