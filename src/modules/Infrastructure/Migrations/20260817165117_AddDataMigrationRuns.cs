using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataMigrationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorageKind",
                table: "SUBMISSION_FILES",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MANAGED");

            migrationBuilder.CreateTable(
                name: "DATA_MIGRATION_RUNS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TemplateId = table.Column<long>(type: "bigint", nullable: false),
                    UnmappedColumns = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    MappedColumnCount = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OptionsJson = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    RequestedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    ImportedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    FilesImported = table.Column<int>(type: "int", nullable: false),
                    FilesMissing = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DATA_MIGRATION_RUNS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DATA_MIGRATION_RECORDS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<long>(type: "bigint", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SurveyId = table.Column<long>(type: "bigint", nullable: true),
                    SurveyCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SubmissionId = table.Column<long>(type: "bigint", nullable: true),
                    FilesImported = table.Column<int>(type: "int", nullable: false),
                    FilesMissing = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DATA_MIGRATION_RECORDS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DATA_MIGRATION_RECORDS_DATA_MIGRATION_RUNS_RunId",
                        column: x => x.RunId,
                        principalTable: "DATA_MIGRATION_RUNS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DATA_MIGRATION_RECORDS_ExternalId",
                table: "DATA_MIGRATION_RECORDS",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_DATA_MIGRATION_RECORDS_RunId_Status_RowNumber",
                table: "DATA_MIGRATION_RECORDS",
                columns: new[] { "RunId", "Status", "RowNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DATA_MIGRATION_RUNS_SourceCode_Status",
                table: "DATA_MIGRATION_RUNS",
                columns: new[] { "SourceCode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DATA_MIGRATION_RECORDS");

            migrationBuilder.DropTable(
                name: "DATA_MIGRATION_RUNS");

            migrationBuilder.DropColumn(
                name: "StorageKind",
                table: "SUBMISSION_FILES");
        }
    }
}
