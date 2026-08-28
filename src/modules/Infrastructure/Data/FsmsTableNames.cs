namespace KH.Infrastructure.Data;

/// <summary>
/// Physical table names for the FSMS module. The database is FSMS-only, so table names
/// carry no <c>FSMS_</c> prefix — the prefix would repeat what the database already says.
/// </summary>
public static class FsmsTableNames
{
    public const string Templates = "TEMPLATES";
    public const string TemplateVersions = "TEMPLATE_VERSIONS";
    public const string FieldCatalog = "FIELD_CATALOG";
    public const string Teams = "TEAMS";
    public const string TeamSchedules = "TEAM_SCHEDULES";

    /// <summary>Departments a crew works for; replaces the comma-separated <c>TEAMS.Departments</c>.</summary>
    public const string TeamDepartments = "TEAM_DEPARTMENTS";


    public const string LookupDepartment = "LKP_DEPARTMENT";
    public const string LookupFaType = "LKP_FA_TYPE";
    public const string LookupTaskType = "LKP_TASK_TYPE";
    public const string LookupCustomerType = "LKP_CUSTOMER_TYPE";
    public const string LookupContractor = "LKP_CONTRACTOR";

    // The org geography: Cluster → CBU → (Branch | Operation Area). The last two are siblings, both
    // parented by the CBU. Each level names its parent by code rather than by id, because every
    // already speaks in codes.
    public const string LookupCluster = "LKP_CLUSTER";
    public const string LookupCbu = "LKP_CBU";
    public const string LookupBranch = "LKP_BRANCH";
    public const string LookupOperationArea = "LKP_OPERATION_AREA";

    /// <summary>
    /// Which parts of the geography a team, user or template covers. One table for all three:
    /// the question asked of each is the same, and splitting it would mean three copies of the
    /// downward expansion logic.
    /// </summary>
    public const string OrgScopes = "ORG_SCOPES";

    /// <summary>Why a reviewer returned a survey for rework; seeded and operator-extensible.</summary>
    public const string LookupReturnReason = "LKP_RETURN_REASON";

    /// <summary>Survey instances — one row per survey to be carried out (task-03).</summary>
    public const string Surveys = "SURVEYS";

    /// <summary>Append-only status trail of a survey; one row per transition.</summary>
    public const string SurveyStatusHistory = "SURVEY_STATUS_HISTORY";

    /// <summary>Allocation of a survey to a field team (task-04).</summary>
    public const string SurveyAssignments = "SURVEY_ASSIGNMENTS";

    /// <summary>
    /// The single shared submission table for every template. Base columns plus one nullable
    /// column per catalog <c>data_name</c>; managed by native SQL (no EF entity / migration).
    /// </summary>
    public const string Submissions = "SUBMISSIONS";

    /// <summary>Uploaded media tracked per file and linked to a submission on submit (EF-managed).</summary>
    public const string SubmissionFiles = "SUBMISSION_FILES";

    /// <summary>One import of an external data set — source, file, options and running totals.</summary>
    public const string DataMigrationRuns = "DATA_MIGRATION_RUNS";

    /// <summary>What became of each record in an import run; the run's own audit trail.</summary>
    public const string DataMigrationRecords = "DATA_MIGRATION_RECORDS";

    /// <summary>
    /// Legacy prefix of the retired per-template submission tables (<c>SUB_{TemplateId}</c>).
    /// Those tables are no longer written or read; kept only so historical rows stay reachable.
    /// </summary>
    public const string LegacySubmissionPrefix = "SUB_";
}
