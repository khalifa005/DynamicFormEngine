using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractorAndTeamContractorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContractorId",
                table: "TEAMS",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LKP_CONTRACTOR",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    CommercialRegistration = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LKP_CONTRACTOR", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TEAMS_ContractorId",
                table: "TEAMS",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_LKP_CONTRACTOR_PoNumber",
                table: "LKP_CONTRACTOR",
                column: "PoNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TEAMS_LKP_CONTRACTOR_ContractorId",
                table: "TEAMS",
                column: "ContractorId",
                principalTable: "LKP_CONTRACTOR",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TEAMS_LKP_CONTRACTOR_ContractorId",
                table: "TEAMS");

            migrationBuilder.DropTable(
                name: "LKP_CONTRACTOR");

            migrationBuilder.DropIndex(
                name: "IX_TEAMS_ContractorId",
                table: "TEAMS");

            migrationBuilder.DropColumn(
                name: "ContractorId",
                table: "TEAMS");
        }
    }
}
