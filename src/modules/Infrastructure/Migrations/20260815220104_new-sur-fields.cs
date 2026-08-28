using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class newsurfields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "SURVEYS",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustomerTypeId",
                table: "SURVEYS",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Hcn",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExternalTask",
                table: "SURVEYS",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MeterNumber",
                table: "SURVEYS",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TaskTypeId",
                table: "SURVEYS",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LKP_CUSTOMER_TYPE",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LKP_CUSTOMER_TYPE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LKP_TASK_TYPE",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LKP_TASK_TYPE", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_CustomerTypeId",
                table: "SURVEYS",
                column: "CustomerTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_IsExternalTask",
                table: "SURVEYS",
                column: "IsExternalTask");

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_TaskTypeId",
                table: "SURVEYS",
                column: "TaskTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LKP_CUSTOMER_TYPE_Code",
                table: "LKP_CUSTOMER_TYPE",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LKP_TASK_TYPE_Code",
                table: "LKP_TASK_TYPE",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LKP_CUSTOMER_TYPE");

            migrationBuilder.DropTable(
                name: "LKP_TASK_TYPE");

            migrationBuilder.DropIndex(
                name: "IX_SURVEYS_CustomerTypeId",
                table: "SURVEYS");

            migrationBuilder.DropIndex(
                name: "IX_SURVEYS_IsExternalTask",
                table: "SURVEYS");

            migrationBuilder.DropIndex(
                name: "IX_SURVEYS_TaskTypeId",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "CustomerTypeId",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "Hcn",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "IsExternalTask",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "MeterNumber",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "TaskTypeId",
                table: "SURVEYS");
        }
    }
}
