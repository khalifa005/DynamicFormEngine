using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Lookups;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Surveys.Queries.GetSurveys;

/// <summary>The survey worklist — server-side paged and filtered.</summary>
[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetSurveysQuery : IRequest<Result<PagedResult<SurveyListItemDto>>>
{
    /// <summary>Accepted between statuses sent in one value — <c>?statuses=ASSIGNED,RETURNED</c>.</summary>
    private const char StatusSeparator = ',';

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Narrows to one lifecycle state. Superseded by <see cref="Statuses"/> and kept so existing
    /// clients keep working; both are honoured together.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Narrows to any of several lifecycle states — see <see cref="SurveyStatuses"/>. Repeat the key
    /// (<c>?statuses=ASSIGNED&amp;statuses=RETURNED</c>) or send one comma-separated value.
    /// </summary>
    public IReadOnlyList<string> Statuses { get; init; } = [];

    public string? Source { get; init; }
    public string? ClusterCode { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }
    public string? FaTypeCode { get; init; }
    public bool? IsExternalTask { get; init; }
    public long? TemplateId { get; init; }
    public long? FieldTeamId { get; init; }

    /// <summary>Narrows to surveys sent back for one particular cause.</summary>
    public string? ReturnReasonCode { get; init; }

    /// <summary>Inclusive lower bound on the survey's creation date.</summary>
    public DateTimeOffset? CreatedFrom { get; init; }

    /// <summary>Inclusive upper bound on the survey's creation date.</summary>
    public DateTimeOffset? CreatedTo { get; init; }

    /// <summary>Matches survey code, FA id or task code.</summary>
    public string? Search { get; init; }

    /// <summary>
    /// The statuses actually being filtered on, from both filter properties at once.
    /// </summary>
    public IReadOnlyList<string> ResolveStatuses() =>
        [.. (Statuses ?? [])
            .Append(Status)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(
                StatusSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(value => value.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)];
}

public sealed class GetSurveysQueryValidator : AbstractValidator<GetSurveysQuery>
{
    public GetSurveysQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 1000).WithMessage("Page size must be between 1 and 1000.");

        RuleFor(x => x)
            .Must(x => x.ResolveStatuses().All(SurveyStatuses.IsDefined))
            .WithMessage($"Status must be one of: {string.Join(", ", SurveyStatuses.All)}.");

        RuleFor(x => x.Source)
            .Must(SurveySources.IsDefined).WithMessage("Source is not a recognized survey source.")
            .When(x => !string.IsNullOrWhiteSpace(x.Source));

        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom!.Value)
            .WithMessage("The end of the date range must not precede its start.")
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);
    }
}

/// <summary>
/// Reads the worklist in three steps rather than one query:
///
/// 1. A flat projection over <c>Surveys</c> alone, selecting only the columns the list renders.
///    Projecting the entity instead would drag in <c>AdditionalDataJson</c> and
///    <c>ResultSummaryJson</c> — two <c>nvarchar(max)</c> columns the list never shows, and
///    usually the bulk of the bytes on the wire.
/// 2. The reference-data labels, from the shared lookup name cache. No database work at all.
/// 3. The template and allocated-team names, batched by the ids the page actually used.
///
/// Resolving all of those inline instead — as correlated subqueries in the projection — costs a
/// nested OUTER APPLY per lookup per row, which is what made this read slow. The page now costs
/// one filtered read plus two keyed lookups, regardless of page size.
/// </summary>
public sealed class GetSurveysQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetSurveysQuery, Result<PagedResult<SurveyListItemDto>>>
{
    public async Task<Result<PagedResult<SurveyListItemDto>>> Handle(
        GetSurveysQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Surveys.AsNoTracking();

        var statuses = request.ResolveStatuses();
        if (statuses.Count == 1)
        {
            var status = statuses[0];
            query = query.Where(x => x.Status == status);
        }
        else if (statuses.Count > 1)
        {
            var selected = statuses.ToList();
            query = query.Where(x => EF.Parameter(selected).Contains(x.Status));
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim();
            query = query.Where(x => x.Source == source);
        }

        if (!string.IsNullOrWhiteSpace(request.ClusterCode) || !string.IsNullOrWhiteSpace(request.CbuCode))
        {
            var hierarchy = await OrgHierarchyCache.GetAsync(context, cache, cacheSettings.Value, cancellationToken);

            var cbuCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(request.ClusterCode))
            {
                foreach (var code in hierarchy.CbusUnderCluster(request.ClusterCode.Trim()))
                {
                    cbuCodes.Add(code);
                }
            }

            if (!string.IsNullOrWhiteSpace(request.CbuCode))
            {
                cbuCodes.Add(request.CbuCode.Trim());
            }

            var cbuCodeList = cbuCodes.ToList();
            query = query.Where(x => x.CbuCode != null && cbuCodeList.Contains(x.CbuCode));
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var branchCode = request.BranchCode.Trim();
            query = query.Where(x => x.BranchCode == branchCode);
        }

        if (!string.IsNullOrWhiteSpace(request.OperationAreaCode))
        {
            var operationAreaCode = request.OperationAreaCode.Trim();
            query = query.Where(x => x.OperationAreaCode == operationAreaCode);
        }

        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        if (!callerScope.IsUnrestricted)
        {
            query = query.Where(OrgScopeQueryFilter.ForSurveys(callerScope));
        }

        if (request.DepartmentId is int departmentId)
        {
            query = query.Where(x => x.DepartmentId == departmentId);
        }

        if (!string.IsNullOrWhiteSpace(request.FaTypeCode))
        {
            var faTypeCode = request.FaTypeCode.Trim();
            query = query.Where(x => x.FaTypeCode == faTypeCode);
        }

        if (request.IsExternalTask is bool isExternalTask)
        {
            query = query.Where(x => x.IsExternalTask == isExternalTask);
        }

        if (request.TemplateId is long templateId)
        {
            query = query.Where(x => x.TemplateId == templateId);
        }

        if (request.FieldTeamId is long fieldTeamId)
        {
            // Deliberately the same expression as the allocated-team column below, not `Any(active)`.
            // The two must agree: filtering on a team and finding none of the surveys that visibly
            // name that team is the kind of inconsistency an operator reads as lost data. Active-only
            // here would have hidden every completed survey — all 923 approved imports among them.
            query = query.Where(x => x.Assignments
                .OrderByDescending(a => a.IsActive)
                .ThenByDescending(a => a.AssignedDate)
                .Select(a => (long?)a.FieldTeamId)
                .FirstOrDefault() == fieldTeamId);
        }

        if (!string.IsNullOrWhiteSpace(request.ReturnReasonCode))
        {
            var returnReasonCode = request.ReturnReasonCode.Trim();
            query = query.Where(x => x.ReturnReasonCode == returnReasonCode);
        }

        if (request.CreatedFrom is DateTimeOffset createdFrom)
        {
            query = query.Where(x => x.Created >= createdFrom);
        }

        if (request.CreatedTo is DateTimeOffset createdTo)
        {
            query = query.Where(x => x.Created <= createdTo);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.SurveyCode.Contains(search) ||
                x.FaId != null && x.FaId.Contains(search) ||
                x.TaskCode != null && x.TaskCode.Contains(search) ||
                x.CustomerName != null && x.CustomerName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            // Newest first: the worklist is read from the top.
            .OrderByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new SurveyListRow
            {
                SurveyId = x.Id,
                SurveyCode = x.SurveyCode,
                TemplateId = x.TemplateId,
                TemplateVersionNo = x.TemplateVersionNo,
                Source = x.Source,
                Status = x.Status,
                FaId = x.FaId,
                TaskCode = x.TaskCode,
                FaTypeCode = x.FaTypeCode,
                TaskTypeId = x.TaskTypeId,
                CustomerName = x.CustomerName,
                CustomerPhone = x.CustomerPhone,
                CustomerTypeId = x.CustomerTypeId,
                MeterNumber = x.MeterNumber,
                Hcn = x.Hcn,
                IsExternalTask = x.IsExternalTask,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                CbuCode = x.CbuCode,
                BranchCode = x.BranchCode,
                OperationAreaCode = x.OperationAreaCode,
                DepartmentId = x.DepartmentId,
                // The live allocation when there is one, otherwise the crew that last held it.
                //
                // Active-only was the original rule, and it left every APPROVED survey showing no
                // team at all: Survey.Complete calls Approve() on the live assignment, which
                // deactivates it. For live work that reads as "nobody holds this", which is true —
                // but the column is read as "which crew did this survey", and that answer does not
                // stop being true when the work is signed off. Imported records made it obvious,
                // since an import closes most of them on arrival and the whole grid came back blank
                // in a column the operator had just filled in.
                //
                // Ordering on IsActive first keeps the old behaviour exactly where it applied: a
                // survey re-allocated after a submission still shows the crew holding it now, not
                // the one before.
                AllocatedFieldTeamId = x.Assignments
                    .OrderByDescending(a => a.IsActive)
                    .ThenByDescending(a => a.AssignedDate)
                    .Select(a => (long?)a.FieldTeamId)
                    .FirstOrDefault(),
                DueDate = x.DueDate,
                CompletionDueDate = x.CompletionDueDate,
                TeamFillSlaHours = x.TeamFillSlaHours,
                CompletionSlaHours = x.CompletionSlaHours,
                AssignedDate = x.AssignedDate,
                SubmittedDate = x.SubmittedDate,
                LastFilledByRole = x.LastFilledByRole,
                SubmissionCount = x.SubmissionCount,
                ReturnReasonCode = x.ReturnReasonCode,
                ReturnReason = x.ReturnReason,
                ReturnedDate = x.ReturnedDate,
                ReturnCount = x.ReturnCount,
                Created = x.Created,
                LastModified = x.LastModified,
            })
            .ToListAsync(cancellationToken);

        // A page past the end has nothing to label, and on a cold cache the lookup loads below
        // would otherwise read both reference tables to decorate no rows at all.
        if (rows.Count == 0)
        {
            return Result<PagedResult<SurveyListItemDto>>.Success(
                new PagedResult<SurveyListItemDto>([], totalCount, request.Page, request.PageSize));
        }

        var cbuNames = await LookupNameCache.GetCbuNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var operationAreaNames = await LookupNameCache.GetOperationAreaNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var departmentNames = await LookupNameCache.GetDepartmentNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var taskTypeNames = await LookupNameCache.GetTaskTypeNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var customerTypeNames = await LookupNameCache.GetCustomerTypeNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var templates = await LoadTemplatesAsync(rows, cancellationToken);
        var teamNames = await LoadTeamNamesAsync(rows, cancellationToken);

        var items = rows
            .Select(row =>
            {
                var cbu = cbuNames.FindCbu(row.CbuCode);
                var branch = branchNames.FindBranch(row.BranchCode);
                var operationArea = operationAreaNames.FindOperationArea(row.OperationAreaCode);
                var department = departmentNames.FindDepartment(row.DepartmentId);
                var taskType = taskTypeNames.FindTaskType(row.TaskTypeId);
                var customerType = customerTypeNames.FindCustomerType(row.CustomerTypeId);
                var template = templates.GetValueOrDefault(row.TemplateId);

                return new SurveyListItemDto
                {
                    SurveyId = row.SurveyId,
                    SurveyCode = row.SurveyCode,
                    TemplateId = row.TemplateId,
                    TemplateCode = template?.TemplateCode,
                    TemplateNameEn = template?.TemplateNameEn,
                    TemplateNameAr = template?.TemplateNameAr,
                    TemplateVersionNo = row.TemplateVersionNo,
                    TemplateCurrentVersionNo = template?.CurrentVersionNo,
                    Source = row.Source,
                    Status = row.Status,
                    FaId = row.FaId,
                    TaskCode = row.TaskCode,
                    FaTypeCode = row.FaTypeCode,
                    TaskTypeId = row.TaskTypeId,
                    TaskTypeNameEn = taskType?.NameEn,
                    TaskTypeNameAr = taskType?.NameAr,
                    CustomerName = row.CustomerName,
                    CustomerPhone = row.CustomerPhone,
                    CustomerTypeId = row.CustomerTypeId,
                    CustomerTypeNameEn = customerType?.NameEn,
                    CustomerTypeNameAr = customerType?.NameAr,
                    MeterNumber = row.MeterNumber,
                    Hcn = row.Hcn,
                    IsExternalTask = row.IsExternalTask,
                    Latitude = row.Latitude,
                    Longitude = row.Longitude,
                    CbuCode = row.CbuCode,
                    CbuNameEn = cbu?.NameEn,
                    CbuNameAr = cbu?.NameAr,
                    BranchCode = row.BranchCode,
                    BranchNameEn = branch?.NameEn,
                    BranchNameAr = branch?.NameAr,
                    OperationAreaCode = row.OperationAreaCode,
                    OperationAreaNameEn = operationArea?.NameEn,
                    OperationAreaNameAr = operationArea?.NameAr,
                    DepartmentId = row.DepartmentId,
                    DepartmentNameEn = department?.NameEn,
                    DepartmentNameAr = department?.NameAr,
                    AllocatedFieldTeamId = row.AllocatedFieldTeamId,
                    AllocatedFieldTeamName = row.AllocatedFieldTeamId is long teamId
                        ? teamNames.GetValueOrDefault(teamId)
                        : null,
                    DueDate = row.DueDate,
                    CompletionDueDate = row.CompletionDueDate,
                    TeamFillSlaHours = row.TeamFillSlaHours,
                    CompletionSlaHours = row.CompletionSlaHours,
                    AssignedDate = row.AssignedDate,
                    SubmittedDate = row.SubmittedDate,
                    LastFilledByRole = row.LastFilledByRole,
                    SubmissionCount = row.SubmissionCount,
                    ReturnReasonCode = row.ReturnReasonCode,
                    ReturnReason = row.ReturnReason,
                    ReturnedDate = row.ReturnedDate,
                    ReturnCount = row.ReturnCount,
                    Created = row.Created,
                    LastModified = row.LastModified,
                };
            })
            .ToList();

        var paged = new PagedResult<SurveyListItemDto>(items, totalCount, request.Page, request.PageSize);

        return Result<PagedResult<SurveyListItemDto>>.Success(paged);
    }

    /// <summary>
    /// Templates for the ids on this page, keyed by id. Left-joining per row would list a survey
    /// whose template was archived away; so does this, since a missing id simply has no entry.
    /// </summary>
    private async Task<Dictionary<long, TemplateLabel>> LoadTemplatesAsync(
        List<SurveyListRow> rows,
        CancellationToken cancellationToken)
    {
        var templateIds = rows.Select(x => x.TemplateId).Distinct().ToList();

        if (templateIds.Count == 0)
        {
            return [];
        }

        return await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => templateIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TemplateCode, x.TemplateNameEn, x.TemplateNameAr, x.CurrentVersionNo })
            .ToDictionaryAsync(
                x => x.Id,
                x => new TemplateLabel(x.TemplateCode, x.TemplateNameEn, x.TemplateNameAr, x.CurrentVersionNo),
                cancellationToken);
    }

    /// <summary>Names of the teams allocated on this page, keyed by team id.</summary>
    private async Task<Dictionary<long, string>> LoadTeamNamesAsync(
        List<SurveyListRow> rows,
        CancellationToken cancellationToken)
    {
        var teamIds = rows
            .Where(x => x.AllocatedFieldTeamId.HasValue)
            .Select(x => x.AllocatedFieldTeamId!.Value)
            .Distinct()
            .ToList();

        if (teamIds.Count == 0)
        {
            return [];
        }

        return await context.FieldTeams
            .AsNoTracking()
            .Where(x => teamIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

}

/// <summary>
/// The survey columns the list renders, before its labels are resolved. Internal rather than
/// nested and private: EF materializes a projection through generated accessors, and a private
/// nested target is the kind of thing that compiles cleanly and then fails at runtime.
/// </summary>
internal sealed class SurveyListRow
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public int? TemplateVersionNo { get; init; }
    public string Source { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? FaId { get; init; }
    public string? TaskCode { get; init; }
    public string? FaTypeCode { get; init; }
    public long? TaskTypeId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public long? CustomerTypeId { get; init; }
    public string? MeterNumber { get; init; }
    public string? Hcn { get; init; }
    public bool IsExternalTask { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }
    public long? AllocatedFieldTeamId { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public int? TeamFillSlaHours { get; init; }
    public int? CompletionSlaHours { get; init; }
    public DateTimeOffset? AssignedDate { get; init; }
    public DateTimeOffset? SubmittedDate { get; init; }
    public string? LastFilledByRole { get; init; }
    public int SubmissionCount { get; init; }
    public string? ReturnReasonCode { get; init; }
    public string? ReturnReason { get; init; }
    public DateTimeOffset? ReturnedDate { get; init; }
    public int ReturnCount { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }
}

internal sealed record TemplateLabel(string TemplateCode, string TemplateNameEn, string TemplateNameAr, int? CurrentVersionNo);
