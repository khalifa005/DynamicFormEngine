using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updatesurveyteam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeviceCreatedDate",
                table: "SURVEYS",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceCreatedDate",
                table: "SURVEYS");
        }
    }
}
