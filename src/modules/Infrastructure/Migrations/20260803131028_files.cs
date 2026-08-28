using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class files : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaTypeCode",
                table: "TEMPLATES",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FieldTeamId",
                table: "AspNetUsers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TEMPLATES_FaTypeCode_Status",
                table: "TEMPLATES",
                columns: new[] { "FaTypeCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_FieldTeamId",
                table: "AspNetUsers",
                column: "FieldTeamId",
                filter: "[FieldTeamId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TEMPLATES_FaTypeCode_Status",
                table: "TEMPLATES");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_FieldTeamId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FaTypeCode",
                table: "TEMPLATES");

            migrationBuilder.DropColumn(
                name: "FieldTeamId",
                table: "AspNetUsers");
        }
    }
}
