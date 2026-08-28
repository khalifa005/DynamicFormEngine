namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Names shared between the dashboard slice and <c>sql/procedures/usp_Fsms_Dashboard_Get.sql</c>.
/// The procedure's parameter names are its contract, so they live here rather than as literals
/// scattered through the handler.
/// </summary>
public abstract class DashboardSql
{
    public const string ProcedureName = "dbo.usp_Fsms_Dashboard_Get";

    public abstract class Parameters
    {
        public const string FromDate = "@FromDate";
        public const string ToDate = "@ToDate";
        public const string ClusterCode = "@ClusterCode";
        public const string CbuCode = "@CbuCode";
        public const string BranchCode = "@BranchCode";
        public const string OperationAreaCode = "@OperationAreaCode";
        public const string DepartmentId = "@DepartmentId";
        public const string TemplateId = "@TemplateId";
        public const string FaTypeCode = "@FaTypeCode";
        public const string Status = "@Status";
        public const string Source = "@Source";
        public const string UserId = "@UserId";
        public const string PeerScope = "@PeerScope";
        public const string RoleName = "@RoleName";
        public const string TrendGrain = "@TrendGrain";
        public const string TopN = "@TopN";
        public const string IsUnrestricted = "@IsUnrestricted";
        public const string ScopeGroupsJson = "@ScopeGroupsJson";
    }
}

/// <summary>How the trend chart buckets its x-axis.</summary>
public abstract class DashboardTrendGrains
{
    public const string Day = "DAY";
    public const string Week = "WEEK";
    public const string Month = "MONTH";

    public static readonly IReadOnlyList<string> All = [Day, Week, Month];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Which other people the signed-in user is measured against.</summary>
public abstract class DashboardPeerScopes
{
    /// <summary>Everyone holding the same role — the default reading of "my peers".</summary>
    public const string Role = "ROLE";

    /// <summary>Everyone on the same field team.</summary>
    public const string Team = "TEAM";

    /// <summary>Everyone whose org scope covers the same department.</summary>
    public const string Department = "DEPARTMENT";

    /// <summary>Everyone whose org scope covers the same territory code.</summary>
    public const string Branch = "BRANCH";

    public static readonly IReadOnlyList<string> All = [Role, Team, Department, Branch];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}
