using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrgHierarchyAndScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cluster",
                table: "LKP_ZONE");

            migrationBuilder.AddColumn<string>(
                name: "CbuCode",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationAreaCode",
                table: "SURVEYS",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CbuCode",
                table: "LKP_ZONE",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LKP_CBU",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClusterCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    OrgId = table.Column<long>(type: "bigint", nullable: true),
                    OrgCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultTaskZone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LKP_CBU", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LKP_CLUSTER",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LKP_CLUSTER", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LKP_OPERATION_AREA",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ZoneCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MainAreaCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NameEn = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LKP_OPERATION_AREA", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ORG_SCOPES",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORG_SCOPES", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TEAM_DEPARTMENTS",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<long>(type: "bigint", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TEAM_DEPARTMENTS", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_CbuCode_Status",
                table: "SURVEYS",
                columns: new[] { "CbuCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SURVEYS_OperationAreaCode",
                table: "SURVEYS",
                column: "OperationAreaCode");

            migrationBuilder.CreateIndex(
                name: "IX_LKP_ZONE_CbuCode",
                table: "LKP_ZONE",
                column: "CbuCode");

            migrationBuilder.CreateIndex(
                name: "IX_LKP_CBU_ClusterCode",
                table: "LKP_CBU",
                column: "ClusterCode");

            migrationBuilder.CreateIndex(
                name: "IX_LKP_CBU_Code",
                table: "LKP_CBU",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LKP_CLUSTER_Code",
                table: "LKP_CLUSTER",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LKP_OPERATION_AREA_ZoneCode_Code",
                table: "LKP_OPERATION_AREA",
                columns: new[] { "ZoneCode", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ORG_SCOPES_Owner",
                table: "ORG_SCOPES",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ORG_SCOPES_Owner_Level_Code",
                table: "ORG_SCOPES",
                columns: new[] { "OwnerType", "OwnerId", "Level", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TEAM_DEPARTMENTS_TeamId_DepartmentId",
                table: "TEAM_DEPARTMENTS",
                columns: new[] { "TeamId", "DepartmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LKP_CBU");

            migrationBuilder.DropTable(
                name: "LKP_CLUSTER");

            migrationBuilder.DropTable(
                name: "LKP_OPERATION_AREA");

            migrationBuilder.DropTable(
                name: "ORG_SCOPES");

            migrationBuilder.DropTable(
                name: "TEAM_DEPARTMENTS");

            migrationBuilder.DropIndex(
                name: "IX_SURVEYS_CbuCode_Status",
                table: "SURVEYS");

            migrationBuilder.DropIndex(
                name: "IX_SURVEYS_OperationAreaCode",
                table: "SURVEYS");

            migrationBuilder.DropIndex(
                name: "IX_LKP_ZONE_CbuCode",
                table: "LKP_ZONE");

            migrationBuilder.DropColumn(
                name: "CbuCode",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "OperationAreaCode",
                table: "SURVEYS");

            migrationBuilder.DropColumn(
                name: "CbuCode",
                table: "LKP_ZONE");

            migrationBuilder.AddColumn<string>(
                name: "Cluster",
                table: "LKP_ZONE",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
