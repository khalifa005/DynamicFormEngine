using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataMigrationRunsmissinginfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceColumnCount",
                table: "DATA_MIGRATION_RUNS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnmappedColumnCount",
                table: "DATA_MIGRATION_RUNS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ValidatedCount",
                table: "DATA_MIGRATION_RUNS",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceColumnCount",
                table: "DATA_MIGRATION_RUNS");

            migrationBuilder.DropColumn(
                name: "UnmappedColumnCount",
                table: "DATA_MIGRATION_RUNS");

            migrationBuilder.DropColumn(
                name: "ValidatedCount",
                table: "DATA_MIGRATION_RUNS");
        }
    }
}
