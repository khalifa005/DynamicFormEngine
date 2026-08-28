using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ftinfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppVersion",
                table: "TEAMS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                table: "TEAMS",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceOs",
                table: "TEAMS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceUuid",
                table: "TEAMS",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActiveAt",
                table: "TEAMS",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastActiveLatitude",
                table: "TEAMS",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastActiveLongitude",
                table: "TEAMS",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppVersion",
                table: "TEAMS");

            migrationBuilder.DropColumn(
                name: "DeviceName",
                table: "TEAMS");

            migrationBuilder.DropColumn(
                name: "DeviceOs",
                table: "TEAMS");

            migrationBuilder.DropColumn(
                name: "DeviceUuid",
                table: "TEAMS");

            migrationBuilder.DropColumn(
                name: "LastActiveAt",
                table: "TEAMS");

            migrationBuilder.DropColumn(
                name: "LastActiveLatitude",
                table: "TEAMS");

            migrationBuilder.DropColumn(
                name: "LastActiveLongitude",
                table: "TEAMS");
        }
    }
}
