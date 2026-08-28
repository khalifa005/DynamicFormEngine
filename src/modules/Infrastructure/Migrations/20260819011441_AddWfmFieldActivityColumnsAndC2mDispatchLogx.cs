using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KH.Infrastructure.Migrations
{
    /// <summary>
    /// Zone becomes Branch, and Operation Area re-parents from Branch to CBU.
    /// </summary>
    /// <remarks>
    /// Hand-corrected after scaffolding. EF cannot tell a rename from a drop-and-create, so it
    /// generated <c>DropTable("LKP_ZONE")</c> + <c>CreateTable("LKP_BRANCH")</c>, which would have
    /// destroyed every branch row and orphaned every survey and scope pointing at one. It also
    /// cannot see the two data moves this rename implies:
    ///
    /// <list type="number">
    /// <item><c>LKP_OPERATION_AREA.ZoneCode</c> is renamed to <c>CbuCode</c>, but still holds branch
    /// codes. The values have to be resolved up to the CBU, and that must happen while the branch
    /// rows are still readable.</item>
    /// <item><c>ORG_SCOPES.Level</c> stores the level as text. Rows saying <c>'Zone'</c> match
    /// nothing once the code calls it <c>'Branch'</c>, and every owner scoped at that level would
    /// silently lose their worklist.</item>
    /// </list>
    ///
    /// Order matters: the table rename comes first so the operation-area lookup can read it, and the
    /// operation-area values are resolved before anything relies on them naming a CBU.
    /// </remarks>
    public partial class AddWfmFieldActivityColumnsAndC2mDispatchLogx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── LKP_ZONE → LKP_BRANCH, rows intact ───────────────────────────────────────────
            migrationBuilder.RenameTable(
                name: "LKP_ZONE",
                newName: "LKP_BRANCH");

            migrationBuilder.RenameIndex(
                name: "IX_LKP_ZONE_Code",
                table: "LKP_BRANCH",
                newName: "IX_LKP_BRANCH_Code");

            migrationBuilder.RenameIndex(
                name: "IX_LKP_ZONE_CbuCode",
                table: "LKP_BRANCH",
                newName: "IX_LKP_BRANCH_CbuCode");

            // ── The columns that name a branch ───────────────────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "ZoneScope",
                table: "TEMPLATES",
                newName: "BranchScope");

            migrationBuilder.RenameColumn(
                name: "WorkZone",
                table: "TEAM_SCHEDULES",
                newName: "WorkBranch");

            migrationBuilder.RenameColumn(
                name: "ZoneCode",
                table: "SURVEYS",
                newName: "BranchCode");

            migrationBuilder.RenameIndex(
                name: "IX_SURVEYS_ZoneCode_Status",
                table: "SURVEYS",
                newName: "IX_SURVEYS_BranchCode_Status");

            // ── Operation area re-parents from branch to CBU ─────────────────────────────────
            migrationBuilder.RenameColumn(
                name: "ZoneCode",
                table: "LKP_OPERATION_AREA",
                newName: "CbuCode");

            // The column now holds branch codes under a CBU name. Resolve each one up to the CBU its
            // branch sits in. Rows whose branch no longer exists are left alone rather than nulled —
            // the column is NOT NULL, and a dangling code is visible in the lookups screen where a
            // null would only surface as a crash.
            migrationBuilder.Sql(@"
                UPDATE oa
                SET oa.CbuCode = b.CbuCode
                FROM dbo.LKP_OPERATION_AREA AS oa
                INNER JOIN dbo.LKP_BRANCH AS b ON b.Code = oa.CbuCode
                WHERE b.CbuCode IS NOT NULL;");

            migrationBuilder.RenameIndex(
                name: "IX_LKP_OPERATION_AREA_ZoneCode_Code",
                table: "LKP_OPERATION_AREA",
                newName: "IX_LKP_OPERATION_AREA_CbuCode_Code");

            // ── The scope level is stored as text ────────────────────────────────────────────
            migrationBuilder.Sql("UPDATE dbo.ORG_SCOPES SET Level = N'Branch' WHERE Level = N'Zone';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Everything reverses except one step: resolving an operation area's branch code up to
            // its CBU is lossy, because a CBU holds many branches and the original branch is not
            // recoverable from the CBU alone. Reversing would have to guess, and a silently wrong
            // parent is worse than a migration that refuses to run backwards. Restore from a backup
            // instead — which is also the only safe way to undo this on a database with real data.
            throw new InvalidOperationException(
                "AddWfmFieldActivityColumnsAndC2mDispatchLogx cannot be reverted: it resolves each " +
                "operation area's branch up to a CBU, and the branch it came from is not recoverable. " +
                "Restore the database from the backup taken before applying it.");
        }
    }
}
