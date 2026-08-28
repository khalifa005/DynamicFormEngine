using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Lookups;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Surveys.Queries.GetFieldTeamSyncBundle;

/// <summary>
/// Everything the mobile app needs to work offline, in one call: the crew's open work and the form
/// definitions to render it.
///
/// One round trip is the whole point. Paging the worklist and then fetching each survey's definition
/// separately is dozens of requests on the connection a crew has just before losing it, and any one
/// of them failing leaves the device with a half-filled cache. Definitions are sent once each rather
/// than once per survey — a crew holding thirty jobs of the same form should carry one copy of it.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record GetFieldTeamSyncBundleQuery : IRequest<Result<FieldTeamSyncBundleDto>>
{
    /// <summary>
    /// The <see cref="FieldTeamSyncBundleDto.ServerTime"/> from the app's previous sync. Unset pulls
    /// the whole cache; supplied, only what changed since — which is what makes a routine sync cheap.
    /// </summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>Caps one bundle. See <see cref="FieldTeamSyncBundleDto.HasMore"/>.</summary>
    public int MaxSurveys { get; init; } = 200;
}

public sealed class GetFieldTeamSyncBundleQueryValidator : AbstractValidator<GetFieldTeamSyncBundleQuery>
{
    public const int MaxSurveysLimit = 500;

    public GetFieldTeamSyncBundleQueryValidator()
    {
        RuleFor(x => x.MaxSurveys)
            .InclusiveBetween(1, MaxSurveysLimit)
            .WithMessage($"Max surveys must be between 1 and {MaxSurveysLimit}.");
    }
}

/// <summary>
/// One survey to cache on the device. Carries the ids of the definition to render it with rather
/// than the definition itself — see <see cref="FieldTeamSyncBundleDto.Templates"/>.
/// </summary>
public sealed class FieldTeamSyncSurveyDto
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
    public string? TaskTypeNameEn { get; init; }
    public string? TaskTypeNameAr { get; init; }
    public string? CustomerName { get; init; }

    /// <summary>
    /// The number the crew calls before arriving. Carried in the offline bundle because that call is
    /// most often made from the road, with no connection to fetch it.
    /// </summary>
    public string? CustomerPhone { get; init; }

    public long? CustomerTypeId { get; init; }
    public string? CustomerTypeNameEn { get; init; }
    public string? CustomerTypeNameAr { get; init; }
    public string? MeterNumber { get; init; }
    public string? Hcn { get; init; }
    public bool IsExternalTask { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? OperationAreaNameEn { get; init; }
    public string? OperationAreaNameAr { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }

    /// <summary>The caller's own allocation — the one a fill is recorded against.</summary>
    public long AssignmentId { get; init; }

    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public DateTimeOffset? AssignedDate { get; init; }
    public int SubmissionCount { get; init; }

    /// <summary>Why the back office sent it back, so the crew knows what to correct offline.</summary>
    public string? ReturnReason { get; init; }

    public string? ReturnReasonCode { get; init; }

    /// <summary>The dispatch instruction the source system attached for the crew.</summary>
    public string? SourceComment { get; init; }

    /// <summary>Any context the source system attached when the survey was raised.</summary>
    public string AdditionalDataJson { get; init; } = "{}";

    public DateTimeOffset LastModified { get; init; }
}

/// <summary>
/// One form definition, keyed by the template and version the surveys in this bundle are pinned to.
/// </summary>
public sealed class FieldTeamSyncTemplateDto
{
    public long TemplateId { get; init; }
    public int? VersionNo { get; init; }
    public string? TemplateNameEn { get; init; }
    public string? TemplateNameAr { get; init; }

    /// <summary>
    /// The form-builder definition to render, frozen at the version the surveys are pinned to. A
    /// survey raised before any publish falls back to the template's current definition.
    /// </summary>
    public string DefinitionJson { get; init; } = "{}";
}

public sealed class FieldTeamSyncBundleDto
{
    /// <summary>Send this back as <see cref="GetFieldTeamSyncBundleQuery.Since"/> next time.</summary>
    public DateTimeOffset ServerTime { get; init; }

    /// <summary>
    /// More work changed than one bundle carries. The app syncs again straight away — with the same
    /// <c>Since</c>, since <see cref="ServerTime"/> would skip whatever this bundle left behind.
    /// </summary>
    public bool HasMore { get; init; }

    public IReadOnlyList<FieldTeamSyncSurveyDto> Surveys { get; init; } = [];
    public IReadOnlyList<FieldTeamSyncTemplateDto> Templates { get; init; } = [];

    /// <summary>
    /// Surveys the crew no longer holds — re-allocated, closed or expired while they were offline.
    /// The device drops these from its cache; without them a crew would keep filling work that has
    /// moved on. Only meaningful on a delta sync: a full pull simply replaces the cache.
    /// </summary>
    public IReadOnlyList<long> DroppedSurveyIds { get; init; } = [];
}

public sealed class GetFieldTeamSyncBundleQueryHandler(
    IApplicationDbContext context,
    IUser user,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings,
    TimeProvider timeProvider)
    : IRequestHandler<GetFieldTeamSyncBundleQuery, Result<FieldTeamSyncBundleDto>>
{
    /// <summary>The states a crew can still act on. Anything else is the back office's business.</summary>
    private static readonly string[] FillableStatuses =
    [
        SurveyStatuses.Created,
        SurveyStatuses.Assigned,
        SurveyStatuses.InProgress,
        SurveyStatuses.Returned,
    ];

    public async Task<Result<FieldTeamSyncBundleDto>> Handle(
        GetFieldTeamSyncBundleQuery request,
        CancellationToken cancellationToken)
    {
        if (user.FieldTeamId is not long fieldTeamId)
        {
            return Result<FieldTeamSyncBundleDto>.Fail(
                "This endpoint is only available to a field-team login.",
                ApiErrorCodes.UnauthorizedAccess,
                httpStatusCode: 403);
        }

        // Read before the queries so nothing changed mid-read can fall between this bundle and the
        // next one the app asks for.
        var serverTime = timeProvider.GetUtcNow();

        var held = context.Surveys
            .AsNoTracking()
            .Where(x => x.Assignments.Any(a => a.FieldTeamId == fieldTeamId && a.IsActive));

        var changed = request.Since is DateTimeOffset since
            ? held.Where(x => x.LastModified > since)
            : held;

        var openWork = changed.Where(x => FillableStatuses.Contains(x.Status));

        var rows = await openWork
            // Due first, then newest: the crew's list reads by what runs out of time soonest.
            .OrderBy(x => x.DueDate == null)
            .ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.Id)
            .Take(request.MaxSurveys + 1)
            .Select(x => new SyncSurveyRow
            {
                SurveyId = x.Id,
                SurveyCode = x.SurveyCode,
                TemplateId = x.TemplateId,
                TemplateVersionId = x.TemplateVersionId,
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
                BranchCode = x.BranchCode,
                OperationAreaCode = x.OperationAreaCode,
                DepartmentId = x.DepartmentId,
                AssignmentId = x.Assignments
                    .Where(a => a.FieldTeamId == fieldTeamId && a.IsActive)
                    .OrderByDescending(a => a.AssignedDate)
                    .Select(a => a.Id)
                    .FirstOrDefault(),
                DueDate = x.Assignments
                    .Where(a => a.FieldTeamId == fieldTeamId && a.IsActive)
                    .OrderByDescending(a => a.AssignedDate)
                    .Select(a => a.DueDate)
                    .FirstOrDefault() ?? x.DueDate,
                CompletionDueDate = x.CompletionDueDate,
                AssignedDate = x.AssignedDate,
                SubmissionCount = x.SubmissionCount,
                ReturnReason = x.ReturnReason,
                ReturnReasonCode = x.ReturnReasonCode,
                SourceComment = x.SourceComment,
                AdditionalDataJson = x.AdditionalDataJson,
                LastModified = x.LastModified,
            })
            .ToListAsync(cancellationToken);

        // One over the cap was read purely to answer "is there more".
        var hasMore = rows.Count > request.MaxSurveys;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var dropped = await LoadDroppedAsync(fieldTeamId, request.Since, cancellationToken);
        var templates = await LoadTemplatesAsync(rows, cancellationToken);
        var surveys = await ToDtosAsync(rows, cancellationToken);

        return Result<FieldTeamSyncBundleDto>.Success(new FieldTeamSyncBundleDto
        {
            ServerTime = serverTime,
            HasMore = hasMore,
            Surveys = surveys,
            Templates = templates,
            DroppedSurveyIds = dropped,
        });
    }

    /// <summary>
    /// Work the device is still holding that it should let go of: the allocation ended, or the
    /// survey moved past what a crew can act on. Only a delta sync asks — a full pull rebuilds the
    /// cache outright, so there is nothing stale left to name.
    /// </summary>
    private async Task<IReadOnlyList<long>> LoadDroppedAsync(
        long fieldTeamId,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        if (since is not DateTimeOffset changedSince)
        {
            return [];
        }

        return await context.Surveys
            .AsNoTracking()
            .Where(x => x.LastModified > changedSince
                && x.Assignments.Any(a => a.FieldTeamId == fieldTeamId)
                && (!x.Assignments.Any(a => a.FieldTeamId == fieldTeamId && a.IsActive)
                    || !FillableStatuses.Contains(x.Status)))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The definitions the bundle's surveys are pinned to, one row per distinct template and version.
    /// Prefers the <c>Mobile</c> snapshot when the publish produced one, since that is the shape this
    /// caller renders.
    /// </summary>
    private async Task<IReadOnlyList<FieldTeamSyncTemplateDto>> LoadTemplatesAsync(
        IReadOnlyList<SyncSurveyRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var templateIds = rows.Select(x => x.TemplateId).Distinct().ToList();

        var templates = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => templateIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TemplateNameEn, x.TemplateNameAr, x.DefinitionJson })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var versionIds = rows
            .Where(x => x.TemplateVersionId.HasValue)
            .Select(x => x.TemplateVersionId!.Value)
            .Distinct()
            .ToList();

        var pinned = await context.TemplateVersions
            .AsNoTracking()
            .Where(v => versionIds.Contains(v.Id))
            .Select(v => new { v.Id, v.TemplateId, v.VersionNo, v.SchemaJson })
            .ToListAsync(cancellationToken);

        var pinnedById = pinned.ToDictionary(v => v.Id);

        // A publish writes one snapshot per target client, so the mobile shape of the same version
        // number is fetched alongside and preferred where it exists.
        var mobileByKey = await context.TemplateVersions
            .AsNoTracking()
            .Where(v => templateIds.Contains(v.TemplateId) && v.TargetClient == TargetClients.Mobile)
            .Select(v => new { v.TemplateId, v.VersionNo, v.SchemaJson })
            .ToListAsync(cancellationToken);

        var mobileSchemas = mobileByKey.ToDictionary(v => (v.TemplateId, v.VersionNo), v => v.SchemaJson);

        var bundled = new List<FieldTeamSyncTemplateDto>();
        var seen = new HashSet<(long TemplateId, int? VersionNo)>();

        foreach (var row in rows)
        {
            var versionNo = row.TemplateVersionId is long versionId && pinnedById.TryGetValue(versionId, out var version)
                ? version.VersionNo
                : row.TemplateVersionNo;

            if (!seen.Add((row.TemplateId, versionNo)))
            {
                continue;
            }

            var hasTemplate = templates.TryGetValue(row.TemplateId, out var template);

            var definitionJson =
                (versionNo is int no && mobileSchemas.TryGetValue((row.TemplateId, no), out var mobileSchema)
                    ? mobileSchema
                    : null)
                ?? (row.TemplateVersionId is long id && pinnedById.TryGetValue(id, out var pinnedVersion)
                    ? pinnedVersion.SchemaJson
                    : null)
                ?? (hasTemplate ? template!.DefinitionJson : null)
                ?? "{}";

            bundled.Add(new FieldTeamSyncTemplateDto
            {
                TemplateId = row.TemplateId,
                VersionNo = versionNo,
                TemplateNameEn = hasTemplate ? template!.TemplateNameEn : null,
                TemplateNameAr = hasTemplate ? template!.TemplateNameAr : null,
                DefinitionJson = definitionJson,
            });
        }

        return bundled;
    }

    private async Task<IReadOnlyList<FieldTeamSyncSurveyDto>> ToDtosAsync(
        IReadOnlyList<SyncSurveyRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var operationAreaNames = await LookupNameCache.GetOperationAreaNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var departmentNames = await LookupNameCache.GetDepartmentNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var taskTypeNames = await LookupNameCache.GetTaskTypeNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var customerTypeNames = await LookupNameCache.GetCustomerTypeNamesAsync(context, cache, cacheSettings.Value, cancellationToken);

        return rows
            .Select(row =>
            {
                var branch = branchNames.FindBranch(row.BranchCode);
                var operationArea = operationAreaNames.FindOperationArea(row.OperationAreaCode);
                var department = departmentNames.FindDepartment(row.DepartmentId);
                var taskType = taskTypeNames.FindTaskType(row.TaskTypeId);
                var customerType = customerTypeNames.FindCustomerType(row.CustomerTypeId);

                return new FieldTeamSyncSurveyDto
                {
                    SurveyId = row.SurveyId,
                    SurveyCode = row.SurveyCode,
                    TemplateId = row.TemplateId,
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
                    DueDate = row.DueDate,
                    CompletionDueDate = row.CompletionDueDate,
                    AssignedDate = row.AssignedDate,
                    SubmissionCount = row.SubmissionCount,
                    ReturnReason = row.ReturnReason,
                    ReturnReasonCode = row.ReturnReasonCode,
                    SourceComment = row.SourceComment,
                    AdditionalDataJson = row.AdditionalDataJson,
                    LastModified = row.LastModified,
                };
            })
            .ToList();
    }
}

/// <summary>
/// The survey columns the bundle needs before its labels are resolved. Internal rather than nested
/// and private: EF materializes a projection through generated accessors, and a private nested
/// target compiles cleanly and then fails at runtime.
/// </summary>
internal sealed class SyncSurveyRow
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public long? TemplateVersionId { get; init; }
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
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }
    public long AssignmentId { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public DateTimeOffset? AssignedDate { get; init; }
    public int SubmissionCount { get; init; }
    public string? ReturnReason { get; init; }
    public string? ReturnReasonCode { get; init; }
    public string? SourceComment { get; init; }
    public string AdditionalDataJson { get; init; } = "{}";
    public DateTimeOffset LastModified { get; init; }
}
