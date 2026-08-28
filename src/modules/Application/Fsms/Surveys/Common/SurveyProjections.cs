using System.Text.Json;
using KH.Application.Common.Interfaces;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Entities.Fsms.Surveys;

namespace KH.Application.Fsms.Surveys.Common;

/// <summary>
/// Shapes a loaded <see cref="Survey"/> into its API contract. Lifecycle commands all answer with
/// the same detail document, so the join onto the template and the pinned version lives here once.
/// </summary>
internal static class SurveyProjections
{
    /// <summary>The roll-up's per-fill account stamp — see <see cref="Survey.ResultSummaryJson"/>.</summary>
    private const string FilledByProperty = "filledBy";

    private static readonly IReadOnlyDictionary<string, string> NoUserNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Loads a survey with the children the detail contract needs.</summary>
    public static IQueryable<Survey> IncludeDetail(this IQueryable<Survey> surveys) =>
        surveys.Include(x => x.Assignments);

    /// <summary>
    /// Builds the detail document, resolving the template names and the definition frozen at the
    /// pinned version. A survey created before any publish falls back to the live definition.
    /// </summary>
    /// <param name="identityService">
    /// Optional account directory. Supplied, the document also carries the display name of every
    /// account id it names, so a reader shows "filled by Ahmed" rather than a bare id. Left out by
    /// the lifecycle commands: their answer is consumed as a status, not rendered as a record, and
    /// the lookup would be a query per write for names nobody reads.
    /// </param>
    public static async Task<SurveyDetailDto> ToDetailDtoAsync(
        this Survey survey,
        IApplicationDbContext context,
        CancellationToken cancellationToken,
        IIdentityService? identityService = null)
    {
        var template = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => x.Id == survey.TemplateId)
            .Select(x => new
            {
                x.TemplateCode,
                x.TemplateNameEn,
                x.TemplateNameAr,
                x.DefinitionJson,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var pinnedDefinition = survey.TemplateVersionId is long versionId
            ? await context.TemplateVersions
                .AsNoTracking()
                .Where(v => v.Id == versionId)
                .Select(v => v.SchemaJson)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var teamNames = await LoadTeamNamesAsync(survey, context, cancellationToken);
        var cbu = await LoadCbuAsync(survey.CbuCode, context, cancellationToken);
        var branch = await LoadBranchAsync(survey.BranchCode, context, cancellationToken);
        var operationArea = await LoadOperationAreaAsync(survey.OperationAreaCode, context, cancellationToken);
        var department = await LoadDepartmentAsync(survey.DepartmentId, context, cancellationToken);
        var taskType = await LoadTaskTypeAsync(survey.TaskTypeId, context, cancellationToken);
        var customerType = await LoadCustomerTypeAsync(survey.CustomerTypeId, context, cancellationToken);
        var userNames = identityService is null
            ? NoUserNames
            : await identityService.GetUserNamesAsync(CollectUserIds(survey), cancellationToken);

        return new SurveyDetailDto
        {
            SurveyId = survey.Id,
            SurveyCode = survey.SurveyCode,
            TemplateId = survey.TemplateId,
            TemplateCode = template?.TemplateCode,
            TemplateNameEn = template?.TemplateNameEn,
            TemplateNameAr = template?.TemplateNameAr,
            TemplateVersionId = survey.TemplateVersionId,
            TemplateVersionNo = survey.TemplateVersionNo,
            DefinitionJson = pinnedDefinition ?? template?.DefinitionJson ?? "{}",
            Source = survey.Source,
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
            CbuCode = survey.CbuCode,
            CbuNameEn = cbu?.NameEn,
            CbuNameAr = cbu?.NameAr,
            BranchCode = survey.BranchCode,
            BranchNameEn = branch?.NameEn,
            BranchNameAr = branch?.NameAr,
            OperationAreaCode = survey.OperationAreaCode,
            OperationAreaNameEn = operationArea?.NameEn,
            OperationAreaNameAr = operationArea?.NameAr,
            DepartmentId = survey.DepartmentId,
            DepartmentNameEn = department?.NameEn,
            DepartmentNameAr = department?.NameAr,
            DueDate = survey.DueDate,
            CompletionDueDate = survey.CompletionDueDate,
            TeamFillSlaHours = survey.TeamFillSlaHours,
            CompletionSlaHours = survey.CompletionSlaHours,
            SourceComment = survey.SourceComment,
            AdditionalDataJson = survey.AdditionalDataJson,
            ResultSummaryJson = survey.ResultSummaryJson,
            AssignedBy = survey.AssignedBy,
            AssignedDate = survey.AssignedDate,
            StartedDate = survey.StartedDate,
            SubmittedDate = survey.SubmittedDate,
            LastFilledByRole = survey.LastFilledByRole,
            SubmissionCount = survey.SubmissionCount,
            ReceivedBy = survey.ReceivedBy,
            ReceivedDate = survey.ReceivedDate,
            CompletedBy = survey.CompletedBy,
            CompletedDate = survey.CompletedDate,
            ReturnReason = survey.ReturnReason,
            ReturnReasonCode = survey.ReturnReasonCode,
            ReturnedBy = survey.ReturnedBy,
            ReturnedDate = survey.ReturnedDate,
            ReturnCount = survey.ReturnCount,
            Created = survey.Created,
            CreatedBy = survey.CreatedBy,
            LastModified = survey.LastModified,
            LastModifiedBy = survey.LastModifiedBy,
            IsActive = survey.IsActive,
            UserNames = userNames,
            Assignments = survey.Assignments
                .OrderByDescending(a => a.AssignedDate)
                .Select(a => new SurveyAssignmentDto
                {
                    AssignmentId = a.Id,
                    SurveyId = survey.Id,
                    FieldTeamId = a.FieldTeamId,
                    FieldTeamName = teamNames.GetValueOrDefault(a.FieldTeamId),
                    Status = a.Status,
                    AssignedBy = a.AssignedBy,
                    AssignedDate = a.AssignedDate,
                    DueDate = a.DueDate,
                    StartedDate = a.StartedDate,
                    SubmittedDate = a.SubmittedDate,
                    Note = a.Note,
                    IsActive = a.IsActive,
                })
                .ToList(),
        };
    }

    /// <summary>
    /// Every account id the detail document names: the lifecycle stamps on the survey itself, plus
    /// the fills' own stamps, which live inside the roll-up rather than on a column.
    /// </summary>
    private static IReadOnlyList<string?> CollectUserIds(Survey survey)
    {
        List<string?> ids =
        [
            survey.AssignedBy,
            survey.ReceivedBy,
            survey.CompletedBy,
            survey.ReturnedBy,
            survey.CreatedBy,
            survey.LastModifiedBy,
        ];

        ids.AddRange(CollectFillUserIds(survey.ResultSummaryJson));
        return ids;
    }

    /// <summary>
    /// The <c>filledBy</c> of each fill in the roll-up. A roll-up that will not parse costs the
    /// reader the fills' names, not the whole detail document — the digest is shown as stored either
    /// way, and the ids remain readable there.
    /// </summary>
    private static List<string> CollectFillUserIds(string? resultSummaryJson)
    {
        var ids = new List<string>();

        if (string.IsNullOrWhiteSpace(resultSummaryJson))
        {
            return ids;
        }

        try
        {
            using var document = JsonDocument.Parse(resultSummaryJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ids;
            }

            foreach (var role in document.RootElement.EnumerateObject())
            {
                if (role.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var fill in role.Value.EnumerateArray())
                {
                    if (fill.ValueKind == JsonValueKind.Object
                        && fill.TryGetProperty(FilledByProperty, out var filledBy)
                        && filledBy.ValueKind == JsonValueKind.String
                        && filledBy.GetString() is { Length: > 0 } id)
                    {
                        ids.Add(id);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Nothing to resolve; the caller falls back to the raw ids.
        }

        return ids;
    }

    private static async Task<Dictionary<long, string>> LoadTeamNamesAsync(
        Survey survey,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var teamIds = survey.Assignments.Select(a => a.FieldTeamId).Distinct().ToList();
        if (teamIds.Count == 0)
        {
            return [];
        }

        return await context.FieldTeams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
    }

    private static async Task<(string NameEn, string NameAr)?> LoadBranchAsync(
        string? branchCode,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branchCode))
        {
            return null;
        }

        var branch = await context.FsmsBranches
            .AsNoTracking()
            .Where(z => z.Code == branchCode)
            .Select(z => new { z.NameEn, z.NameAr })
            .FirstOrDefaultAsync(cancellationToken);

        return branch is null ? null : (branch.NameEn, branch.NameAr);
    }

    private static async Task<(string NameEn, string NameAr)?> LoadCbuAsync(
        string? cbuCode,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cbuCode))
        {
            return null;
        }

        var cbu = await context.FsmsCbus
            .AsNoTracking()
            .Where(c => c.Code == cbuCode)
            .Select(c => new { c.NameEn, c.NameAr })
            .FirstOrDefaultAsync(cancellationToken);

        return cbu is null ? null : (cbu.NameEn, cbu.NameAr);
    }

    private static async Task<(string NameEn, string NameAr)?> LoadOperationAreaAsync(
        string? operationAreaCode,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operationAreaCode))
        {
            return null;
        }

        var area = await context.FsmsOperationAreas
            .AsNoTracking()
            .Where(a => a.Code == operationAreaCode)
            .Select(a => new { a.NameEn, a.NameAr })
            .FirstOrDefaultAsync(cancellationToken);

        return area is null ? null : (area.NameEn, area.NameAr);
    }

    private static async Task<(string NameEn, string NameAr)?> LoadDepartmentAsync(
        int? departmentId,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (departmentId is not int id)
        {
            return null;
        }

        var department = await context.FsmsDepartments
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new { d.NameEn, d.NameAr })
            .FirstOrDefaultAsync(cancellationToken);

        return department is null ? null : (department.NameEn, department.NameAr);
    }

    private static async Task<(string NameEn, string NameAr)?> LoadTaskTypeAsync(
        long? taskTypeId,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (taskTypeId is not long id)
        {
            return null;
        }

        var taskType = await context.FsmsTaskTypes
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { t.NameEn, t.NameAr })
            .FirstOrDefaultAsync(cancellationToken);

        return taskType is null ? null : (taskType.NameEn, taskType.NameAr);
    }

    private static async Task<(string NameEn, string NameAr)?> LoadCustomerTypeAsync(
        long? customerTypeId,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        if (customerTypeId is not long id)
        {
            return null;
        }

        var customerType = await context.FsmsCustomerTypes
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { t.NameEn, t.NameAr })
            .FirstOrDefaultAsync(cancellationToken);

        return customerType is null ? null : (customerType.NameEn, customerType.NameAr);
    }
}
