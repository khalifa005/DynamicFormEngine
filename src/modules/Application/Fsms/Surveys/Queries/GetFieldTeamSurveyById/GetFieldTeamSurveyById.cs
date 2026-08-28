using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Lookups;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Surveys.Queries.GetFieldTeamSurveyById;

/// <summary>
/// Everything the mobile app needs to open one survey for filling, in a single read: the survey
/// itself, the form definition frozen at its pinned version, and the answers already given.
/// A crew on site is often on a thin connection, so three round-trips to open a form is three
/// chances to fail.
///
/// Only a survey allocated to the team on the caller's token is returned, including work that has
/// since closed as <c>APPROVED</c> or <c>EXPIRED</c> while still allocated to this crew. Anything
/// else — another crew's work, work re-allocated away, or a survey that does not exist — answers
/// identically as "not found", so the API cannot be walked to discover what other crews were given.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record GetFieldTeamSurveyByIdQuery : IRequest<Result<FieldTeamSurveyDetailDto>>
{
    public long SurveyId { get; init; }
}

public sealed class GetFieldTeamSurveyByIdQueryValidator : AbstractValidator<GetFieldTeamSurveyByIdQuery>
{
    public GetFieldTeamSurveyByIdQueryValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");
    }
}

/// <summary>The previous fill, ready to seed the form. See <see cref="HasFill"/> before reading the answers.</summary>
public sealed class FieldTeamSurveyFillDto
{
    /// <summary>
    /// False for a survey nobody has filled yet — which an empty <see cref="Answers"/> alone cannot
    /// express, since a fill where every answer was left blank looks identical.
    /// </summary>
    public bool HasFill { get; init; }

    public long SubmissionId { get; init; }

    /// <summary>See <see cref="FilledByRoles"/> — whether the crew or the back office entered these.</summary>
    public string? FilledByRole { get; init; }

    public DateTimeOffset? SubmittedDate { get; init; }

    /// <summary>Answers keyed by field <c>data_name</c>, exactly as stored.</summary>
    public IReadOnlyDictionary<string, object?> Answers { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}

public sealed class FieldTeamSurveyDetailDto
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public string? TemplateNameEn { get; init; }
    public string? TemplateNameAr { get; init; }
    public int? TemplateVersionNo { get; init; }

    /// <summary>
    /// The form-builder definition to render, frozen at the version the survey is pinned to. A
    /// survey raised before any publish falls back to the template's current definition.
    /// </summary>
    public string DefinitionJson { get; init; } = "{}";

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
    public string? BranchCode { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? OperationAreaNameEn { get; init; }
    public string? OperationAreaNameAr { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }

    /// <summary>The caller's own allocation — the id a fill is recorded against.</summary>
    public long AssignmentId { get; init; }

    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public DateTimeOffset? AssignedDate { get; init; }
    public DateTimeOffset? SubmittedDate { get; init; }
    public int SubmissionCount { get; init; }

    /// <summary>Why the back office sent it back, so the crew knows what to correct.</summary>
    public string? ReturnReason { get; init; }

    /// <summary>Structured cause of the current return — a <c>LKP_RETURN_REASON</c> code.</summary>
    public string? ReturnReasonCode { get; init; }

    /// <summary>English label of that code from the lookup. Null when the survey was never returned.</summary>
    public string? ReturnReasonNameEn { get; init; }

    /// <summary>Arabic label of that code from the lookup. Null when the survey was never returned.</summary>
    public string? ReturnReasonNameAr { get; init; }

    /// <summary>The dispatch instruction the source system attached for the crew.</summary>
    public string? SourceComment { get; init; }

    /// <summary>Any context the source system attached when the survey was raised.</summary>
    public string AdditionalDataJson { get; init; } = "{}";

    public FieldTeamSurveyFillDto LatestFill { get; init; } = new();
}

public sealed class GetFieldTeamSurveyByIdQueryHandler(
    IApplicationDbContext context,
    ISurveySubmissionStore submissionStore,
    IUser user,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetFieldTeamSurveyByIdQuery, Result<FieldTeamSurveyDetailDto>>
{
    public async Task<Result<FieldTeamSurveyDetailDto>> Handle(
        GetFieldTeamSurveyByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (user.FieldTeamId is not long fieldTeamId)
        {
            return Result<FieldTeamSurveyDetailDto>.Fail(
                "This endpoint is only available to a field-team login.",
                ApiErrorCodes.UnauthorizedAccess,
                httpStatusCode: 403);
        }

        var survey = await context.Surveys
            .AsNoTracking()
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        var assignment = survey?.Assignments
            .Where(a => a.FieldTeamId == fieldTeamId && a.Status != AssignmentStatuses.Reassigned)
            .OrderByDescending(a => a.AssignedDate)
            .FirstOrDefault();

        // A survey that exists but was never handed to this crew — or was later taken off it — is
        // reported exactly as one that does not exist. Distinguishing them would confirm the survey
        // is real. Closed work this crew still holds (approved / expired) is returned.
        if (survey is null || assignment is null)
        {
            return Result<FieldTeamSurveyDetailDto>.Fail(
                "Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var template = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => x.Id == survey.TemplateId)
            .Select(x => new { x.TemplateNameEn, x.TemplateNameAr, x.DefinitionJson })
            .FirstOrDefaultAsync(cancellationToken);

        var pinnedDefinition = survey.TemplateVersionId is long versionId
            ? await context.TemplateVersions
                .AsNoTracking()
                .Where(v => v.Id == versionId)
                .Select(v => v.SchemaJson)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var latestFill = await LoadLatestFillAsync(survey.TemplateId, survey.Id, cancellationToken);

        var branchNames = await LookupNameCache.GetBranchNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);
        var operationAreaNames = await LookupNameCache.GetOperationAreaNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);
        var departmentNames = await LookupNameCache.GetDepartmentNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);
        var returnReasonNames = await LookupNameCache.GetReturnReasonNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);
        var taskTypeNames = await LookupNameCache.GetTaskTypeNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);
        var customerTypeNames = await LookupNameCache.GetCustomerTypeNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);

        var branch = branchNames.FindBranch(survey.BranchCode);
        var operationArea = operationAreaNames.FindOperationArea(survey.OperationAreaCode);
        var department = departmentNames.FindDepartment(survey.DepartmentId);
        var returnReason = returnReasonNames.FindReturnReason(survey.ReturnReasonCode);
        var taskType = taskTypeNames.FindTaskType(survey.TaskTypeId);
        var customerType = customerTypeNames.FindCustomerType(survey.CustomerTypeId);

        return Result<FieldTeamSurveyDetailDto>.Success(new FieldTeamSurveyDetailDto
        {
            SurveyId = survey.Id,
            SurveyCode = survey.SurveyCode,
            TemplateId = survey.TemplateId,
            TemplateNameEn = template?.TemplateNameEn,
            TemplateNameAr = template?.TemplateNameAr,
            TemplateVersionNo = survey.TemplateVersionNo,
            DefinitionJson = pinnedDefinition ?? template?.DefinitionJson ?? "{}",
            Status = survey.Status,
            FaId = survey.FaId,
            TaskCode = survey.TaskCode,
            FaTypeCode = survey.FaTypeCode,
            TaskTypeId = survey.TaskTypeId,
            TaskTypeNameEn = taskType?.NameEn,
            TaskTypeNameAr = taskType?.NameAr,
            CustomerName = survey.CustomerName,
            CustomerPhone = survey.CustomerPhone,
            CustomerTypeId = survey.CustomerTypeId,
            CustomerTypeNameEn = customerType?.NameEn,
            CustomerTypeNameAr = customerType?.NameAr,
            MeterNumber = survey.MeterNumber,
            Hcn = survey.Hcn,
            IsExternalTask = survey.IsExternalTask,
            Latitude = survey.Latitude,
            Longitude = survey.Longitude,
            BranchCode = survey.BranchCode,
            BranchNameEn = branch?.NameEn,
            BranchNameAr = branch?.NameAr,
            OperationAreaCode = survey.OperationAreaCode,
            OperationAreaNameEn = operationArea?.NameEn,
            OperationAreaNameAr = operationArea?.NameAr,
            DepartmentId = survey.DepartmentId,
            DepartmentNameEn = department?.NameEn,
            DepartmentNameAr = department?.NameAr,
            AssignmentId = assignment.Id,
            DueDate = assignment.DueDate ?? survey.DueDate,
            CompletionDueDate = survey.CompletionDueDate,
            AssignedDate = assignment.AssignedDate,
            SubmittedDate = survey.SubmittedDate,
            SubmissionCount = survey.SubmissionCount,
            ReturnReason = survey.ReturnReason,
            ReturnReasonCode = survey.ReturnReasonCode,
            ReturnReasonNameEn = returnReason?.NameEn,
            ReturnReasonNameAr = returnReason?.NameAr,
            SourceComment = survey.SourceComment,
            AdditionalDataJson = survey.AdditionalDataJson,
            LatestFill = latestFill,
        });
    }

    private async Task<FieldTeamSurveyFillDto> LoadLatestFillAsync(
        long templateId,
        long surveyId,
        CancellationToken cancellationToken)
    {
        var row = await submissionStore.GetLatestBySurveyAsync(templateId, surveyId, cancellationToken);

        // A survey nobody has filled yet is a valid answer, not a failure — the form opens blank.
        if (row is null)
        {
            return new FieldTeamSurveyFillDto();
        }

        return new FieldTeamSurveyFillDto
        {
            HasFill = true,
            SubmissionId = row.GetValueOrDefault(SubmissionColumns.Id) is long id ? id : 0,
            FilledByRole = row.GetValueOrDefault(SubmissionColumns.FilledByRole) as string,
            SubmittedDate = row.GetValueOrDefault(SubmissionColumns.SubmittedDate) as DateTimeOffset?,
            // Everything that is not one of the fixed columns is a template field.
            Answers = row
                .Where(kvp => !SubmissionColumns.IsBase(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal),
        };
    }
}
