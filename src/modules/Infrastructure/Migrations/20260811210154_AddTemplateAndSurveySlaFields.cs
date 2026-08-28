using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateAndSurveySlaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompletionSlaHours",
                table: "TEMPLATES",
                type: "int",
                nullable: false,
                defaultValue: 48);

            migrationBuilder.AddColumn<int>(
                name: "TeamFillSlaHours",
                table: "TEMPLATES",
                type: "int",
                nullable: false,
                defaultValue: 72);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletionDueDate",
                table: "SURVEYS",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletionSlaHours",
                table: "SURVEYS",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TeamFillSlaHours",
                table: "SURVEYS",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionSlaHours",
                table: "TEMPLATES");

            migrationBuilder.DropColumn(
                name: "TeamFillSlaHours",
                table: "TEMPLATES");

            migrationBuilder.DropColumn(
                name: "CompletionDueDate",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "CompletionSlaHours",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "TeamFillSlaHours",
                table: "SURVEYS");
        }
    }
}
