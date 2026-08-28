using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddC2mInboundLogAndActionMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "C2M_INBOUND_LOGS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FaId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FaTypeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SurveyId = table.Column<long>(type: "bigint", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_C2M_INBOUND_LOGS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LKP_C2M_ACTION_MAPPING",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FaStatus = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LKP_C2M_ACTION_MAPPING", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_C2M_INBOUND_LOGS_FaId",
                table: "C2M_INBOUND_LOGS",
                column: "FaId");

            migrationBuilder.CreateIndex(
                name: "IX_C2M_INBOUND_LOGS_Status_Created",
                table: "C2M_INBOUND_LOGS",
                columns: new[] { "Status", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_LKP_C2M_ACTION_MAPPING_ActionCode",
                table: "LKP_C2M_ACTION_MAPPING",
                column: "ActionCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "C2M_INBOUND_LOGS");

            migrationBuilder.DropTable(
                name: "LKP_C2M_ACTION_MAPPING");
        }
    }
}
