using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fsms_RolesAndReturnReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnCount",
                table: "SURVEYS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReturnReasonCode",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnedBy",
                table: "SURVEYS",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReturnedDate",
                table: "SURVEYS",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LKP_RETURN_REASON",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LKP_RETURN_REASON", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LKP_RETURN_REASON_Code",
                table: "LKP_RETURN_REASON",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LKP_RETURN_REASON");

            migrationBuilder.DropColumn(
                name: "ReturnCount",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "ReturnReasonCode",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "ReturnedBy",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "ReturnedDate",
                table: "SURVEYS");
        }
    }
}
