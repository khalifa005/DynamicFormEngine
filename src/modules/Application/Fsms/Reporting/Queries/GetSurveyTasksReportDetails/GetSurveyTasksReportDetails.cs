using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Lookups;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Reporting.Models;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Reporting.Queries.GetSurveyTasksReportDetails;

/// <summary>
/// The row-by-row table behind the "Survey Tasks" report — true server-side pagination over the
/// same filters the summary (<c>GetSurveyTasksReportSummaryQuery</c>) applies, kept as a separate
/// query so opening the table never means fetching every filtered survey just to show one page of it.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewReports)]
public record GetSurveyTasksReportDetailsQuery : IRequest<Result<PagedResult<SurveyTaskReportRowDto>>>
{
    /// <summary>Inclusive lower bound on <see cref="Survey.Created"/>.</summary>
    public DateTimeOffset? FromDate { get; init; }

    /// <summary>Inclusive upper bound on <see cref="Survey.Created"/>.</summary>
    public DateTimeOffset? ToDate { get; init; }

    /// <summary>See <see cref="SurveyStatuses"/>.</summary>
    public string? Status { get; init; }

    /// <summary>
    /// Narrows to the team holding a survey's most recent assignment — not necessarily the active
    /// one, since an approved survey's allocation is no longer active.
    /// </summary>
    public long? TeamId { get; init; }

    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }

    /// <summary>See <see cref="SurveySources"/>.</summary>
    public string? Source { get; init; }

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;

    /// <summary>See <see cref="SurveyTaskReportSortFields"/>. Defaults to <see cref="SurveyTaskReportSortFields.Created"/>.</summary>
    public string? SortBy { get; init; }

    public bool SortDescending { get; init; } = true;
}

/// <summary>
/// The only columns the detail table may sort by — whitelisted rather than interpolated into
/// <c>OrderBy</c> so an arbitrary client-supplied string never reaches the query.
/// </summary>
public abstract class SurveyTaskReportSortFields
{
    public const string Created = "Created";
    public const string DueDate = "DueDate";
    public const string SurveyCode = "SurveyCode";
    public const string Status = "Status";

    public static readonly IReadOnlyList<string> All = [Created, DueDate, SurveyCode, Status];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}

public sealed class GetSurveyTasksReportDetailsQueryValidator : AbstractValidator<GetSurveyTasksReportDetailsQuery>
{
    public GetSurveyTasksReportDetailsQueryValidator()
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

        RuleFor(x => x.TeamId)
            .GreaterThan(0).WithMessage("Team id must be greater than 0.")
            .When(x => x.TeamId.HasValue);

        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("Page number must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .Must(SurveyTaskReportSortFields.IsDefined)
            .WithMessage($"Sort by must be one of: {string.Join(", ", SurveyTaskReportSortFields.All)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy));
    }
}

public sealed class GetSurveyTasksReportDetailsQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetSurveyTasksReportDetailsQuery, Result<PagedResult<SurveyTaskReportRowDto>>>
{
    public async Task<Result<PagedResult<SurveyTaskReportRowDto>>> Handle(
        GetSurveyTasksReportDetailsQuery request, CancellationToken cancellationToken)
    {
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);
        var filtered = BuildFilteredQuery(context, request, callerScope);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var sorted = ApplySort(filtered, request.SortBy, request.SortDescending);

        var rows = await sorted
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new SurveyTaskReportRow
            {
                SurveyId = x.Id,
                SurveyCode = x.SurveyCode,
                TemplateId = x.TemplateId,
                TemplateVersionNo = x.TemplateVersionNo,
                Status = x.Status,
                Source = x.Source,
                BranchCode = x.BranchCode,
                DepartmentId = x.DepartmentId,
                // The team for a survey is its most recent assignment row, not necessarily the
                // active one — an approved survey's allocation is no longer active.
                TeamId = x.Assignments
                    .OrderByDescending(a => a.AssignedDate)
                    .ThenByDescending(a => a.Id)
                    .Select(a => (long?)a.FieldTeamId)
                    .FirstOrDefault(),
                CustomerName = x.CustomerName,
                FaId = x.FaId,
                Created = x.Created,
                DueDate = x.DueDate,
                SubmissionCount = x.SubmissionCount,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Result<PagedResult<SurveyTaskReportRowDto>>.Success(
                new PagedResult<SurveyTaskReportRowDto>([], totalCount, request.PageNumber, request.PageSize));
        }

        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings.Value, cancellationToken);

        var templateIds = rows.Select(x => x.TemplateId).Distinct().ToList();
        var templates = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => templateIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TemplateNameEn, x.TemplateNameAr })
            .ToDictionaryAsync(x => x.Id, x => (x.TemplateNameEn, x.TemplateNameAr), cancellationToken);

        var teamIds = rows.Where(x => x.TeamId.HasValue).Select(x => x.TeamId!.Value).Distinct().ToList();
        Dictionary<long, string> teamNames = teamIds.Count == 0
            ? []
            : await context.FieldTeams
                .AsNoTracking()
                .Where(x => teamIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name })
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var items = rows
            .Select(row =>
            {
                var branch = branchNames.FindBranch(row.BranchCode);
                var hasTemplate = templates.TryGetValue(row.TemplateId, out var template);

                return new SurveyTaskReportRowDto
                {
                    SurveyId = row.SurveyId,
                    SurveyCode = row.SurveyCode,
                    TemplateNameEn = hasTemplate ? template.TemplateNameEn : null,
                    TemplateNameAr = hasTemplate ? template.TemplateNameAr : null,
                    TemplateVersionNo = row.TemplateVersionNo,
                    Status = row.Status,
                    Source = row.Source,
                    BranchCode = row.BranchCode,
                    BranchNameEn = branch?.NameEn,
                    BranchNameAr = branch?.NameAr,
                    DepartmentId = row.DepartmentId,
                    TeamId = row.TeamId,
                    TeamName = row.TeamId is long teamId && teamNames.TryGetValue(teamId, out var name)
                        ? name
                        : null,
                    CustomerName = row.CustomerName,
                    FaId = row.FaId,
                    Created = row.Created,
                    DueDate = row.DueDate,
                    SubmissionCount = row.SubmissionCount,
                };
            })
            .ToList();

        return Result<PagedResult<SurveyTaskReportRowDto>>.Success(
            new PagedResult<SurveyTaskReportRowDto>(items, totalCount, request.PageNumber, request.PageSize));
    }

    /// <summary>
    /// The filtered, org-scoped survey set the table pages over. Deliberately re-implemented here
    /// rather than shared with the summary query: each slice owns its own request/handler, and the
    /// two filter sets are small enough that duplicating them is cheaper than a cross-slice coupling.
    /// </summary>
    private static IQueryable<Survey> BuildFilteredQuery(
        IApplicationDbContext context,
        GetSurveyTasksReportDetailsQuery request,
        OrgScopeSet callerScope)
    {
        IQueryable<Survey> query = context.Surveys.AsNoTracking();

        if (!callerScope.IsUnrestricted)
        {
            query = query.Where(OrgScopeQueryFilter.ForSurveys(callerScope));
        }

        if (request.FromDate is DateTimeOffset fromDate)
        {
            query = query.Where(x => x.Created >= fromDate);
        }

        if (request.ToDate is DateTimeOffset toDate)
        {
            query = query.Where(x => x.Created <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(x => x.Status == request.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            query = query.Where(x => x.BranchCode == request.BranchCode);
        }

        if (!string.IsNullOrWhiteSpace(request.OperationAreaCode))
        {
            query = query.Where(x => x.OperationAreaCode == request.OperationAreaCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            query = query.Where(x => x.Source == request.Source);
        }

        if (request.TeamId is long teamId)
        {
            query = query.Where(x => x.Assignments
                .OrderByDescending(a => a.AssignedDate)
                .ThenByDescending(a => a.Id)
                .Select(a => (long?)a.FieldTeamId)
                .FirstOrDefault() == teamId);
        }

        return query;
    }

    /// <summary>Applies the whitelisted sort, always with a stable id tie-breaker for deterministic paging.</summary>
    private static IQueryable<Survey> ApplySort(IQueryable<Survey> query, string? sortBy, bool descending)
    {
        var field = SurveyTaskReportSortFields.IsDefined(sortBy) ? sortBy! : SurveyTaskReportSortFields.Created;

        IOrderedQueryable<Survey> ordered = field switch
        {
            SurveyTaskReportSortFields.DueDate => descending
                ? query.OrderByDescending(x => x.DueDate)
                : query.OrderBy(x => x.DueDate),
            SurveyTaskReportSortFields.SurveyCode => descending
                ? query.OrderByDescending(x => x.SurveyCode)
                : query.OrderBy(x => x.SurveyCode),
            SurveyTaskReportSortFields.Status => descending
                ? query.OrderByDescending(x => x.Status)
                : query.OrderBy(x => x.Status),
            _ => descending
                ? query.OrderByDescending(x => x.Created)
                : query.OrderBy(x => x.Created),
        };

        return ordered.ThenByDescending(x => x.Id);
    }
}

/// <summary>
/// The survey columns the detail table needs, before its labels are resolved. Internal rather than
/// nested and private: EF materializes a projection through generated accessors, and a private
/// nested target compiles cleanly and then fails at runtime.
/// </summary>
internal sealed class SurveyTaskReportRow
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public int? TemplateVersionNo { get; init; }
    public string Status { get; init; } = default!;
    public string Source { get; init; } = default!;
    public string? BranchCode { get; init; }
    public int? DepartmentId { get; init; }
    public long? TeamId { get; init; }
    public string? CustomerName { get; init; }
    public string? FaId { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public int SubmissionCount { get; init; }
}
