using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "SURVEYS",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "SURVEYS",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SurveyId",
                table: "SUBMISSION_FILES",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "SurveyId",
                table: "SUBMISSION_FILES");
        }
    }
}
