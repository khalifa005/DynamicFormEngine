namespace KH.Domain.Entities.Fsms.Templates;

/// <summary>
/// Carries the assembled output for one target client into <see cref="SurveyTemplate.Publish"/>.
/// The assembler (task-02) produces these; this phase persists them as <see cref="TemplateVersion"/>
/// rows.
/// </summary>
public sealed record TemplateVersionSnapshot(
    string TargetClient,
    string SchemaJson,
    string TemplateSnapshotJson);
