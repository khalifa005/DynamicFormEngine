using System.Text.Json;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Reporting.Interfaces;
using KH.Application.Fsms.Reporting.Models;
using KH.Domain.Constants.Fsms;
using Microsoft.EntityFrameworkCore;
using Shared.Core.Common;

namespace KH.Application.Fsms.Reporting.Queries.GetGeneralStatisticsReport;

/// <summary>
/// The Reports > General Statistics page. Reads the same filtered survey set the dashboard reads —
/// via <see cref="IDashboardStore"/> — and re-shapes it into this report's own DTO, adding two things
/// the dashboard procedure does not compute: an org-wide active team count and a per-team completion
/// breakdown for the horizontal bar chart. No team filter is exposed: the underlying dashboard
/// aggregation has no team dimension, and per-team drill-down belongs to the separate Team
/// Performance report.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record GetGeneralStatisticsReportQuery : IRequest<Result<GeneralStatisticsReportDto>>
{
    /// <summary>Inclusive lower bound on survey creation.</summary>
    public DateTimeOffset? FromDate { get; init; }

    /// <summary>Inclusive upper bound on survey creation.</summary>
    public DateTimeOffset? ToDate { get; init; }

    public string? ClusterCode { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? Status { get; init; }
    public string? Source { get; init; }
}

public sealed class GetGeneralStatisticsReportQueryValidator : AbstractValidator<GetGeneralStatisticsReportQuery>
{
    public GetGeneralStatisticsReportQueryValidator()
    {
        RuleFor(x => x.ToDate)
            .GreaterThanOrEqualTo(x => x.FromDate!.Value)
            .WithMessage("The end of the date range must not precede its start.")
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue);

        RuleFor(x => x.Status)
            .Must(SurveyStatuses.IsDefined).WithMessage("Status is not a recognized survey status.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Source)
            .Must(SurveySources.IsDefined).WithMessage("Source is not a recognized survey source.")
            .When(x => !string.IsNullOrWhiteSpace(x.Source));
    }
}

/// <summary>
/// Delegates to <see cref="IDashboardStore"/> for the bulk of the numbers (KPIs, trend, status split,
/// recent activity, late work), and runs two supplementary EF Core LINQ queries for what the
/// dashboard procedure does not surface: the org-wide active team count and per-team completion.
/// </summary>
public sealed class GetGeneralStatisticsReportQueryHandler(
    IDashboardStore dashboardStore,
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    IUser user)
    : IRequestHandler<GetGeneralStatisticsReportQuery, Result<GeneralStatisticsReportDto>>
{
    private static readonly JsonSerializerOptions ScopeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<Result<GeneralStatisticsReportDto>> Handle(
        GetGeneralStatisticsReportQuery request,
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
            DepartmentId = null,
            TemplateId = null,
            FaTypeCode = null,
            Status = Clean(request.Status),
            Source = Clean(request.Source),
            UserId = user.Id,
            PeerScope = DashboardPeerScopes.Role,
            RoleName = null,
            TrendGrain = DashboardTrendGrains.Week,
            TopN = 10,
            IsUnrestricted = callerScope.IsUnrestricted,
            ScopeGroupsJson = callerScope.IsUnrestricted ? null : SerializeScope(callerScope),
        };

        var dashboard = await dashboardStore.GetAsync(storeRequest, cancellationToken);

        var totalTeams = await context.FieldTeams
            .AsNoTracking()
            .CountAsync(t => t.IsActive, cancellationToken);

        var teamCompletion = await GetTeamCompletionAsync(request, callerScope, cancellationToken);

        var kpis = new GeneralStatisticsKpisDto
        {
            TotalTasks = dashboard.Kpis.TotalSurveys,
            OverdueTasks = dashboard.Kpis.OverdueSurveys,
            TotalTeams = totalTeams,
            ActiveTeams = dashboard.Kpis.ActiveTeams,
            AvgCompletionHours = dashboard.Kpis.AvgCompletionHours,
            CompletionRatePercent = dashboard.Kpis.CompletionRatePercent,
            ReturnRatePercent = dashboard.Kpis.ReturnRatePercent,
            OnTimeRatePercent = dashboard.Kpis.OnTimeRatePercent,
            TotalTasksDelta = dashboard.Kpis.TotalSurveysDelta,
            OverdueTasksDelta = dashboard.Kpis.OverdueSurveysDelta,
            ActiveTeamsDelta = dashboard.Kpis.ActiveTeamsDelta,
            CompletionRateDelta = dashboard.Kpis.CompletionRateDelta,
            ReturnRateDelta = dashboard.Kpis.ReturnRateDelta,
            OnTimeRateDelta = dashboard.Kpis.OnTimeRateDelta,
        };

        var reportDto = new GeneralStatisticsReportDto
        {
            Kpis = kpis,
            Trend = dashboard.Trend,
            StatusDistribution = dashboard.StatusDistribution,
            TeamCompletion = teamCompletion,
            RecentActivity = dashboard.RecentSurveys.Take(6).ToList(),
            LateTasks = dashboard.LateSurveys,
            LateTeams = dashboard.LateTeams,
        };

        return Result<GeneralStatisticsReportDto>.Success(reportDto);
    }

    /// <summary>
    /// Per-team completion for the horizontal bar chart — a dimension the dashboard procedure does
    /// not break out (it only surfaces per-org-geography stats and "late teams only"). "The team
    /// responsible for a survey" is taken as its most recent assignment rather than its active one,
    /// because <c>SurveyAssignment.IsActive</c> flips to false once a survey is approved and would
    /// otherwise erase every completed survey's team from the breakdown.
    /// </summary>
    private async Task<IReadOnlyList<GeneralStatisticsTeamCompletionDto>> GetTeamCompletionAsync(
        GetGeneralStatisticsReportQuery request,
        OrgScopeSet callerScope,
        CancellationToken cancellationToken)
    {
        var surveys = context.Surveys.AsNoTracking();

        // Every query that lists surveys answers "may this caller see this row" the same way — see
        // OrgScopeQueryFilter's own doc comment. This aggregates surveys by team, so it is scoped
        // like every other survey query rather than by each team's own territory grant.
        if (!callerScope.IsUnrestricted)
        {
            surveys = surveys.Where(OrgScopeQueryFilter.ForSurveys(callerScope));
        }

        if (request.FromDate.HasValue)
        {
            surveys = surveys.Where(s => s.Created >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            surveys = surveys.Where(s => s.Created <= request.ToDate.Value);
        }

        var status = Clean(request.Status);
        if (status is not null)
        {
            surveys = surveys.Where(s => s.Status == status);
        }

        var branchCode = Clean(request.BranchCode);
        if (branchCode is not null)
        {
            surveys = surveys.Where(s => s.BranchCode == branchCode);
        }

        var operationAreaCode = Clean(request.OperationAreaCode);
        if (operationAreaCode is not null)
        {
            surveys = surveys.Where(s => s.OperationAreaCode == operationAreaCode);
        }

        var source = Clean(request.Source);
        if (source is not null)
        {
            surveys = surveys.Where(s => s.Source == source);
        }

        // GroupBy(a => a.SurveyId).Select(g => g.OrderByDescending(...).First()) does not translate
        // to SQL Server (EF throws at runtime, not compile time — verified live). The correlated
        // subquery form off the Survey.Assignments navigation, as GetSurveyTasksReportSummary already
        // uses, does translate.
        var teamStats = await surveys
            .Select(s => new
            {
                s.Status,
                FieldTeamId = s.Assignments
                    .OrderByDescending(a => a.AssignedDate)
                    .ThenByDescending(a => a.Id)
                    .Select(a => (long?)a.FieldTeamId)
                    .FirstOrDefault(),
            })
            .Where(x => x.FieldTeamId != null)
            .GroupBy(x => x.FieldTeamId!.Value)
            .Select(g => new
            {
                FieldTeamId = g.Key,
                Assigned = g.Count(),
                Completed = g.Count(x => x.Status == SurveyStatuses.Approved),
            })
            .ToListAsync(cancellationToken);

        if (teamStats.Count == 0)
        {
            return [];
        }

        var teamIds = teamStats.Select(x => x.FieldTeamId).ToList();

        var teamNames = await context.FieldTeams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return teamStats
            .Select(x => new GeneralStatisticsTeamCompletionDto
            {
                TeamId = x.FieldTeamId,
                TeamName = teamNames.GetValueOrDefault(x.FieldTeamId, string.Empty),
                Assigned = x.Assigned,
                Completed = x.Completed,
                CompletionPercent = x.Assigned == 0 ? 0m : Math.Round(x.Completed * 100m / x.Assigned, 2),
            })
            .OrderByDescending(x => x.CompletionPercent)
            .Take(20)
            .ToList();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Same flattening the dashboard handler uses — see its remarks for why.</summary>
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
