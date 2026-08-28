using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class survey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SURVEYS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    TemplateVersionId = table.Column<long>(type: "bigint", nullable: true),
                    TemplateVersionNo = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FaId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaskCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FaTypeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ZoneCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AdditionalDataJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    ResultSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    AssignedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AssignedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFilledByRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SubmissionCount = table.Column<int>(type: "int", nullable: false),
                    ReceivedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ReceivedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CompletedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReturnReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SURVEYS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SURVEY_ASSIGNMENTS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId = table.Column<long>(type: "bigint", nullable: false),
                    FieldTeamId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AssignedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SURVEY_ASSIGNMENTS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SURVEY_ASSIGNMENTS_SURVEYS_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "SURVEYS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SURVEY_STATUS_HISTORY",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SurveyId = table.Column<long>(type: "bigint", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ChangedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SURVEY_STATUS_HISTORY", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SURVEY_STATUS_HISTORY_SURVEYS_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "SURVEYS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SURVEY_ASSIGNMENTS_FieldTeamId_Status",
                table: "SURVEY_ASSIGNMENTS",
                columns: new[] { "FieldTeamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SURVEY_ASSIGNMENTS_SurveyId",
                table: "SURVEY_ASSIGNMENTS",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_SURVEY_STATUS_HISTORY_SurveyId_ChangedDate",
                table: "SURVEY_STATUS_HISTORY",
                columns: new[] { "SurveyId", "ChangedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_DepartmentId",
                table: "SURVEYS",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_Status",
                table: "SURVEYS",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_SurveyCode",
                table: "SURVEYS",
                column: "SurveyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_TaskCode",
                table: "SURVEYS",
                column: "TaskCode");

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_TemplateId",
                table: "SURVEYS",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_ZoneCode_Status",
                table: "SURVEYS",
                columns: new[] { "ZoneCode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SURVEY_ASSIGNMENTS");

            migrationBuilder.DropTable(
                name: "SURVEY_STATUS_HISTORY");

            migrationBuilder.DropTable(
                name: "SURVEYS");
        }
    }
}
