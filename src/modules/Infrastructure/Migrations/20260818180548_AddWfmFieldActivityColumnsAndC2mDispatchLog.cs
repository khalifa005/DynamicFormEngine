using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWfmFieldActivityColumnsAndC2mDispatchLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WfmAccountId",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WfmBranchCode",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfmBuildUnits",
                table: "SURVEYS",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfmBuildingArea",
                table: "SURVEYS",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WfmBuildingType",
                table: "SURVEYS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfmBuildingUnits",
                table: "SURVEYS",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfmBuildingsCount",
                table: "SURVEYS",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WfmCreatedDate",
                table: "SURVEYS",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WfmCustomerClass",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfmDiameter",
                table: "SURVEYS",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WfmFaStatus",
                table: "SURVEYS",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfmLandArea",
                table: "SURVEYS",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WfmLastModifiedDate",
                table: "SURVEYS",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfmMeterDiameter",
                table: "SURVEYS",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WfmMeterLocation",
                table: "SURVEYS",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WfmMeterStatus",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WfmMeterType",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WfmPropertyType",
                table: "SURVEYS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WfmPurConsumption",
                table: "SURVEYS",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WfmTicketId",
                table: "SURVEYS",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "C2M_DISPATCH_LOGS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId = table.Column<long>(type: "bigint", nullable: false),
                    FaId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OpStatus = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResponseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_C2M_DISPATCH_LOGS", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_FaId",
                table: "SURVEYS",
                column: "FaId");

            migrationBuilder.CreateIndex(
                name: "IX_C2M_DISPATCH_LOGS_Status_FaId",
                table: "C2M_DISPATCH_LOGS",
                columns: new[] { "Status", "FaId" });

            migrationBuilder.CreateIndex(
                name: "IX_C2M_DISPATCH_LOGS_SurveyId",
                table: "C2M_DISPATCH_LOGS",
                column: "SurveyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "C2M_DISPATCH_LOGS");

            migrationBuilder.DropIndex(
                name: "IX_SURVEYS_FaId",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmAccountId",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmBranchCode",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmBuildUnits",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmBuildingArea",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmBuildingType",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmBuildingUnits",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmBuildingsCount",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmCreatedDate",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmCustomerClass",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmDiameter",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmFaStatus",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmLandArea",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmLastModifiedDate",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmMeterDiameter",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmMeterLocation",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmMeterStatus",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmMeterType",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmPropertyType",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmPurConsumption",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "WfmTicketId",
                table: "SURVEYS");
        }
    }
}
