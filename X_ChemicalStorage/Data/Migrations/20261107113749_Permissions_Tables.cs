using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace X_ChemicalStorage.Data.Migrations
{
    /// <inheritdoc />
    public partial class Permissions_Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ERP_Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModuleDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModuleArabicName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModuleIcon = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ModuleOrder = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ERP_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ERP_RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrantedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ERP_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_ERP_RolePermissions_ERP_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "ERP_Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ERP_RolePermissions_ERP_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "ERP_Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ERP_UserPermissions",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    IsGranted = table.Column<bool>(type: "bit", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrantedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ERP_UserPermissions", x => new { x.UserId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_ERP_UserPermissions_ERP_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "ERP_Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ERP_UserPermissions_ERP_WebUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "ERP_WebUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ERP_Permissions_Key",
                table: "ERP_Permissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ERP_Permissions_Module",
                table: "ERP_Permissions",
                column: "Module");

            migrationBuilder.CreateIndex(
                name: "IX_ERP_RolePermissions_PermissionId",
                table: "ERP_RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ERP_UserPermissions_PermissionId",
                table: "ERP_UserPermissions",
                column: "PermissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ERP_RolePermissions");

            migrationBuilder.DropTable(
                name: "ERP_UserPermissions");

            migrationBuilder.DropTable(
                name: "ERP_Permissions");
        }
    }
}
