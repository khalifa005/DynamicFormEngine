using System.Text.Json;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Reporting.Interfaces;
using KH.Application.Fsms.Reporting.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Reporting.Queries.GetDashboard;

/// <summary>The dashboard: KPIs, trends, org/team coverage, and the caller's standing among peers.</summary>
[Authorize(Policy = FsmsPolicies.ViewDashboard)]
public record GetDashboardQuery : IRequest<Result<DashboardDto>>
{
    /// <summary>Inclusive lower bound on survey creation. Defaults to six months back.</summary>
    public DateTimeOffset? FromDate { get; init; }

    /// <summary>Inclusive upper bound on survey creation. Defaults to now.</summary>
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

    /// <summary>How the trend chart buckets time — see <see cref="DashboardTrendGrains"/>.</summary>
    public string? TrendGrain { get; init; }

    /// <summary>Which other people the caller is measured against — see <see cref="DashboardPeerScopes"/>.</summary>
    public string? PeerScope { get; init; }

    /// <summary>Pins the peer cohort to one role. Null compares against every role the caller holds.</summary>
    public string? RoleName { get; init; }

    /// <summary>How many rows the ranked and "recent" lists return.</summary>
    public int TopN { get; init; } = 10;
}

public sealed class GetDashboardQueryValidator : AbstractValidator<GetDashboardQuery>
{
    public GetDashboardQueryValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .WithMessage("The end of the date range must not precede its start.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);

        RuleFor(x => x.TopN)
            .InclusiveBetween(1, 50).WithMessage("Top N must be between 1 and 50.");

        RuleFor(x => x.Status)
            .Must(SurveyStatuses.IsDefined).WithMessage("Status is not a recognized survey status.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Source)
            .Must(SurveySources.IsDefined).WithMessage("Source is not a recognized survey source.")
            .When(x => !string.IsNullOrWhiteSpace(x.Source));

        RuleFor(x => x.TrendGrain)
            .Must(DashboardTrendGrains.IsDefined).WithMessage("Trend grain must be DAY, WEEK or MONTH.")
            .When(x => !string.IsNullOrWhiteSpace(x.TrendGrain));

        RuleFor(x => x.PeerScope)
            .Must(DashboardPeerScopes.IsDefined)
            .WithMessage("Peer scope must be ROLE, TEAM, DEPARTMENT or BRANCH.")
            .When(x => !string.IsNullOrWhiteSpace(x.PeerScope));
    }
}

/// <summary>
/// Resolves the two things the procedure cannot work out for itself — who is asking, and what they
/// are allowed to see — then hands the rest over to the stored procedure.
///
/// The scope is passed as JSON rather than re-derived in SQL on purpose: <see cref="OrgScopeSet"/>
/// already expands a grant downwards through the hierarchy and groups it by department, and a
/// second implementation of that expansion in T-SQL would be the copy that drifts.
/// </summary>
public sealed class GetDashboardQueryHandler(
    IDashboardStore dashboardStore,
    IOrgScopeService orgScopeService,
    IUser user)
    : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    private static readonly JsonSerializerOptions ScopeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<Result<DashboardDto>> Handle(
        GetDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        var storeRequest = new DashboardStoreRequest
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            ClusterCode = Clean(request.ClusterCode),
            CbuCode = Clean(request.CbuCode),
            BranchCode = Clean(request.BranchCode),
            OperationAreaCode = Clean(request.OperationAreaCode),
            DepartmentId = request.DepartmentId,
            TemplateId = request.TemplateId,
            FaTypeCode = Clean(request.FaTypeCode),
            Status = Clean(request.Status),
            Source = Clean(request.Source),
            UserId = user.Id,
            PeerScope = Clean(request.PeerScope) ?? DashboardPeerScopes.Role,
            RoleName = Clean(request.RoleName),
            TrendGrain = Clean(request.TrendGrain) ?? DashboardTrendGrains.Month,
            TopN = request.TopN,
            IsUnrestricted = callerScope.IsUnrestricted,
            ScopeGroupsJson = callerScope.IsUnrestricted ? null : SerializeScope(callerScope),
        };

        var dashboard = await dashboardStore.GetAsync(storeRequest, cancellationToken);

        return Result<DashboardDto>.Success(dashboard);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Flattens the caller's coverage into the shape the procedure shreds with <c>OPENJSON</c>.
    /// Cluster codes are omitted deliberately: <see cref="OrgScopeSet"/> has already resolved every
    /// cluster grant into the CBUs beneath it, and surveys carry no cluster code to match against.
    /// </summary>
    private static string SerializeScope(OrgScopeSet scope)
    {
        var groups = scope.Groups.Select(group => new
        {
            departmentId = group.DepartmentId,
            coversAllTerritory = group.CoversAllTerritory,
            cbuCodes = group.CbuCodes.ToArray(),
            branchCodes = group.BranchCodes.ToArray(),
            operationAreaCodes = group.OperationAreaCodes.ToArray(),
        });

        return JsonSerializer.Serialize(groups, ScopeJsonOptions);
    }
}
