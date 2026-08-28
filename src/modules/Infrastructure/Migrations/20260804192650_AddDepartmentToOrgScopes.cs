using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentToOrgScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ORG_SCOPES_Owner_Level_Code",
                table: "ORG_SCOPES");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "ORG_SCOPES",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "ORG_SCOPES",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "ORG_SCOPES",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ORG_SCOPES_Owner_Level_Code_Department",
                table: "ORG_SCOPES",
                columns: new[] { "OwnerType", "OwnerId", "Level", "Code", "DepartmentId" },
                unique: true,
                filter: "[Level] IS NOT NULL AND [Code] IS NOT NULL AND [DepartmentId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ORG_SCOPES_Owner_Level_Code_Department",
                table: "ORG_SCOPES");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "ORG_SCOPES");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "ORG_SCOPES",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "ORG_SCOPES",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ORG_SCOPES_Owner_Level_Code",
                table: "ORG_SCOPES",
                columns: new[] { "OwnerType", "OwnerId", "Level", "Code" },
                unique: true);
        }
    }
}
