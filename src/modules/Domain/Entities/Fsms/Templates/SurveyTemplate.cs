using KH.Domain.Constants.Fsms;

namespace KH.Domain.Entities.Fsms.Templates;

/// <summary>
/// Aggregate root for a survey template. Stores the form-builder definition as a single JSON
/// document (<see cref="DefinitionJson"/>) and owns its immutable published version snapshots.
/// Table: <c>TEMPLATES</c>.
/// </summary>
public sealed class SurveyTemplate : BaseAuditableEntity<long>
{
    private const string EmptyDefinition = "{}";

    private readonly List<TemplateVersion> _versions = [];

    private SurveyTemplate()
    {
    }

    private SurveyTemplate(
        string templateCode,
        string templateNameEn,
        string templateNameAr,
        string category,
        int? departmentId,
        string? branchScope,
        string? faTypeCode,
        int teamFillSlaHours,
        int completionSlaHours,
        bool autoMigrateSurveysOnPublish)
    {
        TemplateCode = templateCode;
        TemplateNameEn = templateNameEn;
        TemplateNameAr = templateNameAr;
        Category = category;
        DepartmentId = departmentId;
        BranchScope = branchScope;
        FaTypeCode = faTypeCode;
        TeamFillSlaHours = teamFillSlaHours;
        CompletionSlaHours = completionSlaHours;
        AutoMigrateSurveysOnPublish = autoMigrateSurveysOnPublish;
        Status = TemplateStatuses.Draft;
        DefinitionJson = EmptyDefinition;
        IsActive = true;
    }

    public string TemplateCode { get; private set; } = default!;
    public string TemplateNameEn { get; private set; } = default!;
    public string TemplateNameAr { get; private set; } = default!;
    public string Category { get; private set; } = default!;
    public string Status { get; private set; } = default!;
    public int? DepartmentId { get; private set; }
    /// <summary>
    /// Retired comma-separated list of branch codes, superseded by <c>ORG_SCOPES</c> rows owned by
    /// the template. Nothing reads it any more; the column survives one release so the old values
    /// stay recoverable if the scope rows have to be rebuilt.
    /// </summary>
    public string? BranchScope { get; private set; }

    /// <summary>
    /// The kind of field asset this template surveys (see <c>FA_TYPES</c>). It is what an inbound
    /// survey is matched on: the originating system sends the FA type it raised work for, not a
    /// template id, and one published template answers for each type.
    /// </summary>
    public string? FaTypeCode { get; private set; }

    /// <summary>
    /// Calendar hours from allocation until the field team must submit a fill.
    /// Snapshotted onto each survey at create so later template edits do not rewrite in-flight work.
    /// </summary>
    public int TeamFillSlaHours { get; private set; }

    /// <summary>
    /// Calendar hours after the fill window for back-office completion (approve). Together with
    /// <see cref="TeamFillSlaHours"/> this drives the default completion deadline at allocate time.
    /// </summary>
    public int CompletionSlaHours { get; private set; }

    /// <summary>
    /// When true, publishing a new version fans out to this template's own surveys that have not
    /// been filled yet and re-pins them to it. Surveys are pinned precisely so a republish does not
    /// change the form under a crew mid-job, so this is opt-in per template rather than the default.
    /// </summary>
    public bool AutoMigrateSurveysOnPublish { get; private set; }

    public int? CurrentVersionNo { get; private set; }

    /// <summary>
    /// The form-builder "Fulcrum-style" definition document (<c>name_en</c>/<c>name_ar</c>/<c>elements</c>).
    /// This is the single source of truth for the template's fields; the client maps it to formly.
    /// </summary>
    public string DefinitionJson { get; private set; } = EmptyDefinition;

    public IReadOnlyCollection<TemplateVersion> Versions => _versions.AsReadOnly();

    public static SurveyTemplate Create(
        string templateCode,
        string templateNameEn,
        string templateNameAr,
        string category,
        int teamFillSlaHours,
        int completionSlaHours,
        int? departmentId = null,
        string? branchScope = null,
        string? faTypeCode = null,
        bool autoMigrateSurveysOnPublish = false)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
        {
            throw new DomainException("A template must have a code.");
        }

        if (string.IsNullOrWhiteSpace(templateNameEn))
        {
            throw new DomainException("A template must have an English name.");
        }

        if (string.IsNullOrWhiteSpace(templateNameAr))
        {
            throw new DomainException("A template must have an Arabic name.");
        }

        if (!SurveyCategories.IsDefined(category))
        {
            throw new DomainException($"Unknown survey category '{category}'.");
        }

        EnsureValidSlaHours(teamFillSlaHours, completionSlaHours);

        return new SurveyTemplate(
            templateCode.Trim(),
            templateNameEn.Trim(),
            templateNameAr.Trim(),
            category,
            departmentId,
            NormalizeBranchScope(branchScope),
            NormalizeFaTypeCode(faTypeCode),
            teamFillSlaHours,
            completionSlaHours,
            autoMigrateSurveysOnPublish);
    }

    /// <summary>
    /// Toggles automatic survey migration on republish. Deliberately not routed through
    /// <see cref="EnsureEditable"/>: this is a dispatch preference rather than a change to the form,
    /// so flipping it must never reopen a Published template as a Draft revision.
    /// </summary>
    public void SetAutoMigrateSurveysOnPublish(bool enabled) => AutoMigrateSurveysOnPublish = enabled;

    /// <summary>
    /// Updates names, category, SLA, and FA type. Deliberately not routed through
    /// <see cref="EnsureEditable"/>: main-info is not a form-definition change, so a Published
    /// template keeps its status. Deprecated/Archived templates stay blocked.
    /// </summary>
    public void UpdateDetails(
        string templateNameEn,
        string templateNameAr,
        string category,
        int? departmentId,
        string? branchScope,
        int teamFillSlaHours,
        int completionSlaHours,
        string? faTypeCode = null)
    {
        EnsureNotFrozen();

        if (string.IsNullOrWhiteSpace(templateNameEn))
        {
            throw new DomainException("A template must have an English name.");
        }

        if (string.IsNullOrWhiteSpace(templateNameAr))
        {
            throw new DomainException("A template must have an Arabic name.");
        }

        if (!SurveyCategories.IsDefined(category))
        {
            throw new DomainException($"Unknown survey category '{category}'.");
        }

        EnsureValidSlaHours(teamFillSlaHours, completionSlaHours);

        TemplateNameEn = templateNameEn.Trim();
        TemplateNameAr = templateNameAr.Trim();
        Category = category;
        DepartmentId = departmentId;
        BranchScope = NormalizeBranchScope(branchScope);
        FaTypeCode = NormalizeFaTypeCode(faTypeCode);
        TeamFillSlaHours = teamFillSlaHours;
        CompletionSlaHours = completionSlaHours;
    }

    /// <summary>
    /// Replaces the form-builder definition document. Editing a Published template reopens it as a
    /// Draft revision. The template names are synced from the definition when provided.
    /// </summary>
    public void SetDefinition(string definitionJson, string? nameEn, string? nameAr)
    {
        EnsureEditable();

        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            throw new DomainException("A template definition cannot be empty.");
        }

        DefinitionJson = definitionJson;

        if (!string.IsNullOrWhiteSpace(nameEn))
        {
            TemplateNameEn = nameEn.Trim();
        }

        if (!string.IsNullOrWhiteSpace(nameAr))
        {
            TemplateNameAr = nameAr.Trim();
        }
    }

    /// <summary>
    /// Freezes the current draft definition into a version snapshot and marks the template Published.
    /// Existing version rows are never mutated. The caller guarantees the definition has fields.
    /// </summary>
    public void Publish(string? publishedBy, IReadOnlyCollection<TemplateVersionSnapshot> snapshots, DateTimeOffset publishedAt)
    {
        if (Status is TemplateStatuses.Deprecated or TemplateStatuses.Archived)
        {
            throw new DomainException($"A {Status} template cannot be published.");
        }

        if (string.IsNullOrWhiteSpace(DefinitionJson) || DefinitionJson == EmptyDefinition)
        {
            throw new DomainException("A template must have a definition before publishing.");
        }

        if (snapshots is null || snapshots.Count == 0)
        {
            throw new DomainException("At least one version snapshot is required to publish.");
        }

        var newVersionNo = (CurrentVersionNo ?? 0) + 1;

        foreach (var snapshot in snapshots)
        {
            _versions.Add(TemplateVersion.Create(
                newVersionNo,
                snapshot.TargetClient,
                snapshot.SchemaJson,
                snapshot.TemplateSnapshotJson,
                publishedBy,
                publishedAt));
        }

        CurrentVersionNo = newVersionNo;
        Status = TemplateStatuses.Published;
    }

    public void Deprecate()
    {
        if (Status != TemplateStatuses.Published)
        {
            throw new DomainException($"Only a Published template can be deprecated (current: {Status}).");
        }

        Status = TemplateStatuses.Deprecated;
    }

    public void Archive()
    {
        if (Status == TemplateStatuses.Archived)
        {
            throw new DomainException("Template is already archived.");
        }

        Status = TemplateStatuses.Archived;
        IsActive = false;
    }

    /// <summary>
    /// Produces a deep copy as a fresh Draft template under a new code. Version history is not
    /// carried over, and neither is the FA type: one published template answers for a type, so the
    /// clone's own type is a deliberate choice rather than an inherited clash.
    /// </summary>
    public SurveyTemplate Clone(string newTemplateCode, string newTemplateNameEn, string newTemplateNameAr)
    {
        var clone = Create(
            newTemplateCode,
            newTemplateNameEn,
            newTemplateNameAr,
            Category,
            TeamFillSlaHours,
            CompletionSlaHours,
            DepartmentId,
            BranchScope,
            faTypeCode: null,
            AutoMigrateSurveysOnPublish);
        clone.DefinitionJson = DefinitionJson;
        return clone;
    }

    private static void EnsureValidSlaHours(int teamFillSlaHours, int completionSlaHours)
    {
        if (teamFillSlaHours <= 0)
        {
            throw new DomainException("Team fill SLA hours must be greater than zero.");
        }

        if (completionSlaHours <= 0)
        {
            throw new DomainException("Completion SLA hours must be greater than zero.");
        }
    }

    /// <summary>
    /// Blocks edits on Deprecated/Archived templates without changing Status.
    /// </summary>
    private void EnsureNotFrozen()
    {
        if (Status is TemplateStatuses.Deprecated or TemplateStatuses.Archived)
        {
            throw new DomainException($"A {Status} template cannot be edited.");
        }
    }

    /// <summary>
    /// Guards form-definition edits. Editing a Published template opens a new draft revision;
    /// Deprecated/Archived templates are immutable.
    /// </summary>
    private void EnsureEditable()
    {
        EnsureNotFrozen();

        if (Status == TemplateStatuses.Published)
        {
            // Start a new draft revision; existing published versions remain immutable.
            Status = TemplateStatuses.Draft;
        }
    }

    private static string? NormalizeFaTypeCode(string? faTypeCode) =>
        string.IsNullOrWhiteSpace(faTypeCode) ? null : faTypeCode.Trim();

    private static string? NormalizeBranchScope(string? branchScope)
    {
        if (string.IsNullOrWhiteSpace(branchScope))
        {
            return null;
        }

        var codes = branchScope
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return string.Join(',', codes);
    }
}
