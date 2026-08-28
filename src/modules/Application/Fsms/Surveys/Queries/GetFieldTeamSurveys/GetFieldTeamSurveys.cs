using System.Globalization;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Lookups;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Surveys.Queries.GetFieldTeamSurveys;

/// <summary>
/// The crew's own worklist — every survey allocated to the team behind the caller's token. The team
/// is never taken from the request: a mobile client that could name a team id could read another
/// crew's work, so the only team this query will answer for is the one on the token.
///
/// Within that team the app narrows the list three ways: by lifecycle state (one or many at once,
/// so a "needs my attention" tab can ask for <c>ASSIGNED</c>, <c>IN_PROGRESS</c> and
/// <c>RETURNED</c> together), by one of the team's own working scopes, and by free text.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record GetFieldTeamSurveysQuery : IRequest<Result<PagedResult<FieldTeamSurveyListItemDto>>>
{
    /// <summary>Accepted between statuses sent in one value — <c>?statuses=ASSIGNED,RETURNED</c>.</summary>
    private const char StatusSeparator = ',';

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Narrows to one lifecycle state. Superseded by <see cref="Statuses"/> and kept only so clients
    /// written against the single-status filter keep working; both are honoured together.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Narrows to any of several lifecycle states — see <see cref="SurveyStatuses"/>. Repeat the key
    /// (<c>?statuses=ASSIGNED&amp;statuses=RETURNED</c>) or send one comma-separated value; both read
    /// the same. Left unset, every allocated survey is listed.
    /// </summary>
    public IReadOnlyList<string> Statuses { get; init; } = [];

    /// <summary>
    /// Narrows to work sitting inside one of the caller's own working scopes — an <c>ORG_SCOPES</c>
    /// row id, as <c>GET field-team/me</c> reports them. A scope belonging to another team is
    /// refused rather than ignored, so a mistyped id can never silently widen the list.
    ///
    /// The scope's level does not have to match how the survey is stamped: a CBU scope reaches every
    /// branch and operation area beneath it. A scope limited to a department also limits the list to
    /// that department's work, plus work carrying no department at all — the same rule the rest of
    /// the system reads scopes by, so unclassified work is never lost.
    /// </summary>
    public long? ScopeId { get; init; }

    /// <summary>
    /// Widens the list to work this crew has finished with — surveys whose allocation closed as
    /// <c>APPROVED</c> or <c>EXPIRED</c>. They drop off the worklist by default, which is what a
    /// "what do I have to do" list wants; a history screen, or a filter on a closed status, needs
    /// them back. Work re-allocated to another team is never included either way.
    /// </summary>
    public bool IncludeClosed { get; init; }

    /// <summary>Matches survey code, FA id or task code.</summary>
    public string? Search { get; init; }

    /// <summary>
    /// The statuses actually being filtered on, from both filter properties at once. Normalized in
    /// one place so the validator rejects exactly what the handler would have queried.
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

public sealed class GetFieldTeamSurveysQueryValidator : AbstractValidator<GetFieldTeamSurveysQuery>
{
    public GetFieldTeamSurveysQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Page must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 1000).WithMessage("Page size must be between 1 and 1000.");

        // Validated over the combined, normalized set rather than per property: a status is only ever
        // wrong in the same way, and one message naming the whole vocabulary is what a mobile
        // developer can act on.
        RuleFor(x => x)
            .Must(x => x.ResolveStatuses().All(SurveyStatuses.IsDefined))
            .WithMessage($"Status must be one of: {string.Join(", ", SurveyStatuses.All)}.");

        RuleFor(x => x.ScopeId)
            .GreaterThan(0).WithMessage("Scope id must be greater than 0.")
            .When(x => x.ScopeId.HasValue);
    }
}

/// <summary>
/// One row of the crew's worklist — enough to render the list and open the right survey, and no
/// more. The answers, the definition and the fill history all belong to the detail read.
/// </summary>
public sealed class FieldTeamSurveyListItemDto
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public string? TemplateNameEn { get; init; }
    public string? TemplateNameAr { get; init; }
    public int? TemplateVersionNo { get; init; }
    public string Status { get; init; } = default!;
    public string? FaId { get; init; }
    public string? TaskCode { get; init; }
    public string? FaTypeCode { get; init; }
    public long? TaskTypeId { get; init; }
    public string? TaskTypeNameEn { get; init; }
    public string? TaskTypeNameAr { get; init; }
    public string? CustomerName { get; init; }

    /// <summary>The number the crew calls before arriving.</summary>
    public string? CustomerPhone { get; init; }

    public long? CustomerTypeId { get; init; }
    public string? CustomerTypeNameEn { get; init; }
    public string? CustomerTypeNameAr { get; init; }
    public string? MeterNumber { get; init; }
    public string? Hcn { get; init; }
    public bool IsExternalTask { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? OperationAreaNameEn { get; init; }
    public string? OperationAreaNameAr { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }

    /// <summary>The caller's own allocation of this survey — the one a fill is recorded against.</summary>
    public long AssignmentId { get; init; }

    /// <summary>Where that allocation stands — see <see cref="AssignmentStatuses"/>.</summary>
    public string? AssignmentStatus { get; init; }

    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public DateTimeOffset? AssignedDate { get; init; }
    public DateTimeOffset? SubmittedDate { get; init; }
    public int SubmissionCount { get; init; }

    /// <summary>Structured cause of the current return — a <c>LKP_RETURN_REASON</c> code.</summary>
    public string? ReturnReasonCode { get; init; }

    /// <summary>English label of that code from the lookup. Null when the survey was never returned.</summary>
    public string? ReturnReasonNameEn { get; init; }

    /// <summary>Arabic label of that code from the lookup. Null when the survey was never returned.</summary>
    public string? ReturnReasonNameAr { get; init; }

    /// <summary>Why the back office sent the survey back, when it did. Null on work never returned.</summary>
    public string? ReturnReason { get; init; }

    public DateTimeOffset Created { get; init; }
}

public sealed class GetFieldTeamSurveysQueryHandler(
    IApplicationDbContext context,
    IUser user,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetFieldTeamSurveysQuery, Result<PagedResult<FieldTeamSurveyListItemDto>>>
{
    public async Task<Result<PagedResult<FieldTeamSurveyListItemDto>>> Handle(
        GetFieldTeamSurveysQuery request,
        CancellationToken cancellationToken)
    {
        if (user.FieldTeamId is not long fieldTeamId)
        {
            return Result<PagedResult<FieldTeamSurveyListItemDto>>.Fail(
                "This endpoint is only available to a field-team login.",
                ApiErrorCodes.UnauthorizedAccess,
                httpStatusCode: 403);
        }

        // A closed allocation is still this crew's work; one taken off it by a re-allocation is not,
        // so REASSIGNED is excluded in both modes rather than only in the narrow one.
        var query = request.IncludeClosed
            ? context.Surveys
                .AsNoTracking()
                .Where(x => x.Assignments.Any(a =>
                    a.FieldTeamId == fieldTeamId && a.Status != AssignmentStatuses.Reassigned))
            : context.Surveys
                .AsNoTracking()
                .Where(x => x.Assignments.Any(a => a.FieldTeamId == fieldTeamId && a.IsActive));

        var statuses = request.ResolveStatuses();
        if (statuses.Count == 1)
        {
            // One status seeks the status index directly; the IN below cannot.
            var status = statuses[0];
            query = query.Where(x => x.Status == status);
        }
        else if (statuses.Count > 1)
        {
            // EF.Parameter sends the list as a single parameter, so the SQL text — and its cached
            // plan — stays the same however many statuses the app happens to tick.
            var selected = statuses.ToList();
            query = query.Where(x => EF.Parameter(selected).Contains(x.Status));
        }

        if (request.ScopeId is long scopeId)
        {
            var scope = await ResolveScopeAsync(scopeId, fieldTeamId, cancellationToken);
            if (!scope.IsSuccess || scope.Data is null)
            {
                return Result<PagedResult<FieldTeamSurveyListItemDto>>.Fail(scope.Errors);
            }

            query = ApplyScope(query, scope.Data);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.SurveyCode.Contains(search) ||
                x.FaId != null && x.FaId.Contains(search) ||
                x.TaskCode != null && x.TaskCode.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            // Due first, then newest: a crew reads its list by what runs out of time soonest.
            .OrderBy(x => x.DueDate == null)
            .ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new FieldTeamSurveyRow
            {
                SurveyId = x.Id,
                SurveyCode = x.SurveyCode,
                TemplateId = x.TemplateId,
                TemplateVersionNo = x.TemplateVersionNo,
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
                // A team never holds two live allocations of the same survey, so "its own allocation"
                // is the one that was not handed on — whether it is still open or has since closed.
                AssignmentId = x.Assignments
                    .Where(a => a.FieldTeamId == fieldTeamId && a.Status != AssignmentStatuses.Reassigned)
                    .OrderByDescending(a => a.AssignedDate)
                    .Select(a => a.Id)
                    .FirstOrDefault(),
                AssignmentStatus = x.Assignments
                    .Where(a => a.FieldTeamId == fieldTeamId && a.Status != AssignmentStatuses.Reassigned)
                    .OrderByDescending(a => a.AssignedDate)
                    .Select(a => a.Status)
                    .FirstOrDefault(),
                DueDate = x.Assignments
                    .Where(a => a.FieldTeamId == fieldTeamId && a.Status != AssignmentStatuses.Reassigned)
                    .OrderByDescending(a => a.AssignedDate)
                    .Select(a => a.DueDate)
                    .FirstOrDefault() ?? x.DueDate,
                CompletionDueDate = x.CompletionDueDate,
                AssignedDate = x.AssignedDate,
                SubmittedDate = x.SubmittedDate,
                SubmissionCount = x.SubmissionCount,
                ReturnReasonCode = x.ReturnReasonCode,
                ReturnReason = x.ReturnReason,
                Created = x.Created,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Result<PagedResult<FieldTeamSurveyListItemDto>>.Success(
                new PagedResult<FieldTeamSurveyListItemDto>([], totalCount, request.Page, request.PageSize));
        }

        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var operationAreaNames = await LookupNameCache.GetOperationAreaNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var departmentNames = await LookupNameCache.GetDepartmentNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var returnReasonNames = await LookupNameCache.GetReturnReasonNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var taskTypeNames = await LookupNameCache.GetTaskTypeNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var customerTypeNames = await LookupNameCache.GetCustomerTypeNamesAsync(context, cache, cacheSettings.Value, cancellationToken);

        var templateIds = rows.Select(x => x.TemplateId).Distinct().ToList();
        var templates = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => templateIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TemplateNameEn, x.TemplateNameAr })
            .ToDictionaryAsync(x => x.Id, x => (x.TemplateNameEn, x.TemplateNameAr), cancellationToken);

        var items = rows
            .Select(row =>
            {
                var branch = branchNames.FindBranch(row.BranchCode);
                var operationArea = operationAreaNames.FindOperationArea(row.OperationAreaCode);
                var department = departmentNames.FindDepartment(row.DepartmentId);
                var returnReason = returnReasonNames.FindReturnReason(row.ReturnReasonCode);
                var taskType = taskTypeNames.FindTaskType(row.TaskTypeId);
                var customerType = customerTypeNames.FindCustomerType(row.CustomerTypeId);
                var hasTemplate = templates.TryGetValue(row.TemplateId, out var template);

                return new FieldTeamSurveyListItemDto
                {
                    SurveyId = row.SurveyId,
                    SurveyCode = row.SurveyCode,
                    TemplateId = row.TemplateId,
                    TemplateNameEn = hasTemplate ? template.TemplateNameEn : null,
                    TemplateNameAr = hasTemplate ? template.TemplateNameAr : null,
                    TemplateVersionNo = row.TemplateVersionNo,
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
                    BranchCode = row.BranchCode,
                    BranchNameEn = branch?.NameEn,
                    BranchNameAr = branch?.NameAr,
                    OperationAreaCode = row.OperationAreaCode,
                    OperationAreaNameEn = operationArea?.NameEn,
                    OperationAreaNameAr = operationArea?.NameAr,
                    DepartmentId = row.DepartmentId,
                    DepartmentNameEn = department?.NameEn,
                    DepartmentNameAr = department?.NameAr,
                    AssignmentId = row.AssignmentId,
                    AssignmentStatus = row.AssignmentStatus,
                    DueDate = row.DueDate,
                    CompletionDueDate = row.CompletionDueDate,
                    AssignedDate = row.AssignedDate,
                    SubmittedDate = row.SubmittedDate,
                    SubmissionCount = row.SubmissionCount,
                    ReturnReasonCode = row.ReturnReasonCode,
                    ReturnReasonNameEn = returnReason?.NameEn,
                    ReturnReasonNameAr = returnReason?.NameAr,
                    ReturnReason = row.ReturnReason,
                    Created = row.Created,
                };
            })
            .ToList();

        return Result<PagedResult<FieldTeamSurveyListItemDto>>.Success(
            new PagedResult<FieldTeamSurveyListItemDto>(items, totalCount, request.Page, request.PageSize));
    }

    /// <summary>
    /// Turns the working scope the crew picked into the code lists to filter on. Expanded through the
    /// same <see cref="OrgScopeSet"/> the rest of the system reads coverage by, so "everything under
    /// this CBU" means the same here as it does when the back office decides who may see a survey.
    /// </summary>
    private async Task<Result<OrgScopeTerritory>> ResolveScopeAsync(
        long scopeId, long fieldTeamId, CancellationToken cancellationToken)
    {
        var scope = await context.OrgScopes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == scopeId && x.IsActive, cancellationToken);

        if (scope is null)
        {
            return Result<OrgScopeTerritory>.Fail(
                "Working scope not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // The check that stops a crew narrowing — or widening — by another team's scope id.
        if (scope.OwnerType != OrgScopeOwnerTypes.Team
            || scope.OwnerId != fieldTeamId.ToString(CultureInfo.InvariantCulture))
        {
            return Result<OrgScopeTerritory>.Fail(
                "That working scope does not belong to your field team.",
                ApiErrorCodes.UnauthorizedAccess,
                httpStatusCode: 403);
        }

        var hierarchy = await OrgHierarchyCache.GetAsync(context, cache, cacheSettings.Value, cancellationToken);

        // An org unit deleted out from under a scope row would expand to nothing and quietly return
        // an empty page. Said plainly instead — a crew that cannot see its own work needs to know
        // the scope is the reason, not that there is no work.
        if (scope.HasTerritory && hierarchy.Find(scope.Level!, scope.Code!) is null)
        {
            return Result<OrgScopeTerritory>.Fail(
                "The org unit behind this scope no longer exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        var territories = OrgScopeSet
            .FromAssignments(
                [new OrgScopeAssignment { Level = scope.Level, Code = scope.Code, DepartmentId = scope.DepartmentId }],
                hierarchy)
            .ToTerritories();

        // One scope row is one department group, since OrgScope.Create refuses a row that names
        // neither a territory nor a department.
        return territories.Count == 1
            ? Result<OrgScopeTerritory>.Success(territories[0])
            : Result<OrgScopeTerritory>.Fail(
                "This working scope covers nothing.", ApiErrorCodes.ValidationError, httpStatusCode: 400);
    }

    /// <summary>
    /// Narrows the worklist to one scope. A survey only has to be reachable through one of its three
    /// codes, since it is not obliged to carry every level; work carrying no department is admitted
    /// by a department-limited scope, matching <see cref="OrgScopeSet.Covers"/> — hiding unclassified
    /// work would lose it entirely.
    /// </summary>
    private static IQueryable<Survey> ApplyScope(IQueryable<Survey> query, OrgScopeTerritory territory)
    {
        if (!territory.CoversAllTerritory)
        {
            var cbuCodes = territory.CbuCodes.ToList();
            var branchCodes = territory.BranchCodes.ToList();
            var operationAreaCodes = territory.OperationAreaCodes.ToList();

            query = query.Where(x =>
                x.CbuCode != null && EF.Parameter(cbuCodes).Contains(x.CbuCode)
                || x.BranchCode != null && EF.Parameter(branchCodes).Contains(x.BranchCode)
                || x.OperationAreaCode != null && EF.Parameter(operationAreaCodes).Contains(x.OperationAreaCode));
        }

        if (territory.DepartmentId is int departmentId)
        {
            query = query.Where(x => x.DepartmentId == null || x.DepartmentId == departmentId);
        }

        return query;
    }
}

/// <summary>
/// The survey columns the crew's list needs, before its labels are resolved. Internal rather than
/// nested and private: EF materializes a projection through generated accessors, and a private
/// nested target compiles cleanly and then fails at runtime.
/// </summary>
internal sealed class FieldTeamSurveyRow
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public int? TemplateVersionNo { get; init; }
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
    public long AssignmentId { get; init; }
    public string? AssignmentStatus { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public DateTimeOffset? AssignedDate { get; init; }
    public DateTimeOffset? SubmittedDate { get; init; }
    public int SubmissionCount { get; init; }
    public string? ReturnReasonCode { get; init; }
    public string? ReturnReason { get; init; }
    public DateTimeOffset Created { get; init; }
}
