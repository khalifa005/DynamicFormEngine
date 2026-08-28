using KH.Application.Fsms.Common.Org;

namespace KH.Application.Fsms.Templates.Models;

public sealed class TemplateListItemDto
{
    public long TemplateId { get; init; }
    public string TemplateCode { get; init; } = default!;
    public string TemplateNameEn { get; init; } = default!;
    public string TemplateNameAr { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string Status { get; init; } = default!;
    public int? DepartmentId { get; init; }
    public string? BranchScope { get; init; }

    /// <summary>The territory this template may be used in, from <c>ORG_SCOPES</c>.</summary>
    public IReadOnlyList<OrgScopeAssignment> Scopes { get; init; } = [];

    /// <summary>The FA type this template surveys — what an inbound survey is matched on.</summary>
    public string? FaTypeCode { get; init; }

    /// <summary>Calendar hours from allocation until the field team must submit.</summary>
    public int TeamFillSlaHours { get; init; }

    /// <summary>Calendar hours after the fill window for back-office completion.</summary>
    public int CompletionSlaHours { get; init; }

    /// <summary>When true, publishing a new version re-pins this template's unfilled surveys to it.</summary>
    public bool AutoMigrateSurveysOnPublish { get; init; }

    public int? CurrentVersionNo { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastModified { get; init; }
}
