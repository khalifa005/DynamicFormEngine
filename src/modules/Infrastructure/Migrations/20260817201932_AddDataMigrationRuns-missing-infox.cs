using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataMigrationRunsmissinginfox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IgnoredColumnCount",
                table: "DATA_MIGRATION_RUNS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IgnoredColumns",
                table: "DATA_MIGRATION_RUNS",
                type: "NVARCHAR(MAX)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IgnoredColumnCount",
                table: "DATA_MIGRATION_RUNS");

            migrationBuilder.DropColumn(
                name: "IgnoredColumns",
                table: "DATA_MIGRATION_RUNS");
        }
    }
}
