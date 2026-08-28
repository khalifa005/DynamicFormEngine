using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Entities.Fsms.Templates;

namespace KH.Application.Fsms.Templates.Common;

/// <summary>
/// Pure mapping helpers between template domain entities and their DTOs.
/// </summary>
public static class TemplateMappings
{
    public static TemplateListItemDto ToListItemDto(
        this SurveyTemplate template, IReadOnlyList<OrgScopeAssignment>? scopes = null) => new()
    {
        TemplateId = template.Id,
        TemplateCode = template.TemplateCode,
        TemplateNameEn = template.TemplateNameEn,
        TemplateNameAr = template.TemplateNameAr,
        Category = template.Category,
        Status = template.Status,
        DepartmentId = template.DepartmentId,
        BranchScope = template.BranchScope,
        FaTypeCode = template.FaTypeCode,
        TeamFillSlaHours = template.TeamFillSlaHours,
        CompletionSlaHours = template.CompletionSlaHours,
        AutoMigrateSurveysOnPublish = template.AutoMigrateSurveysOnPublish,
        CurrentVersionNo = template.CurrentVersionNo,
        Created = template.Created,
        LastModified = template.LastModified,
        Scopes = scopes ?? []
    };

    public static TemplateDetailDto ToDetailDto(
        this SurveyTemplate template, IReadOnlyList<OrgScopeAssignment>? scopes = null) => new()
    {
        TemplateId = template.Id,
        TemplateCode = template.TemplateCode,
        TemplateNameEn = template.TemplateNameEn,
        TemplateNameAr = template.TemplateNameAr,
        Category = template.Category,
        Status = template.Status,
        DepartmentId = template.DepartmentId,
        BranchScope = template.BranchScope,
        FaTypeCode = template.FaTypeCode,
        TeamFillSlaHours = template.TeamFillSlaHours,
        CompletionSlaHours = template.CompletionSlaHours,
        AutoMigrateSurveysOnPublish = template.AutoMigrateSurveysOnPublish,
        CurrentVersionNo = template.CurrentVersionNo,
        Created = template.Created,
        CreatedBy = template.CreatedBy,
        LastModified = template.LastModified,
        LastModifiedBy = template.LastModifiedBy,
        DefinitionJson = template.DefinitionJson,
        Scopes = scopes ?? []
    };

    public static TemplateVersionDto ToVersionDto(this TemplateVersion version) => new()
    {
        VersionId = version.Id,
        VersionNo = version.VersionNo,
        TargetClient = version.TargetClient,
        SchemaJson = version.SchemaJson,
        TemplateSnapshotJson = version.TemplateSnapshotJson,
        PublishedBy = version.PublishedBy,
        PublishedDate = version.PublishedDate
    };
}
