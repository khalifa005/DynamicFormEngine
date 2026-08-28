using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddC2mInboundFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceComment",
                table: "SURVEYS",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WfmMaintenanceAreaCode",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "SourceComment",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmMaintenanceAreaCode",
                table: "SURVEYS");
        }
    }
}
