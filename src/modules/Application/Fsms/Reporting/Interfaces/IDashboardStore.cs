using KH.Application.Fsms.Reporting.Models;

namespace KH.Application.Fsms.Reporting.Interfaces;

/// <summary>
/// Reads the dashboard from <c>usp_Fsms_Dashboard_Get</c>. Native SQL rather than LINQ because the
/// page needs ten aggregates over one filtered survey set, and two of them — the peer ranking and
/// its percentiles — are window functions EF cannot express. Follows the same shape as
/// <c>ISurveySubmissionStore</c>: the contract lives in Application, the ADO.NET in Infrastructure.
/// </summary>
public interface IDashboardStore
{
    Task<DashboardDto> GetAsync(DashboardStoreRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The procedure's parameters, already resolved: the caller's identity and org coverage are
/// settled by the handler, so the store never has to reason about security.
/// </summary>
public sealed record DashboardStoreRequest
{
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public string? ClusterCode { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }
    public long? TemplateId { get; init; }
    public string? FaTypeCode { get; init; }
    public string? Status { get; init; }
    public string? Source { get; init; }
    public string? UserId { get; init; }
    public string? PeerScope { get; init; }
    public string? RoleName { get; init; }
    public string? TrendGrain { get; init; }
    public int TopN { get; init; } = 10;

    /// <summary>True for a caller whose coverage is not restricted; skips the scope filter entirely.</summary>
    public bool IsUnrestricted { get; init; }

    /// <summary>
    /// The caller's expanded coverage as a JSON array of groups, each pairing one department with
    /// the territory codes it reaches. Null when <see cref="IsUnrestricted"/> is true.
    /// </summary>
    public string? ScopeGroupsJson { get; init; }
}
