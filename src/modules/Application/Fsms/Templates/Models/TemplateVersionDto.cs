namespace KH.Application.Fsms.Templates.Models;

public sealed class TemplateVersionDto
{
    public long VersionId { get; init; }
    public int VersionNo { get; init; }
    public string TargetClient { get; init; } = default!;
    public string SchemaJson { get; init; } = default!;
    public string TemplateSnapshotJson { get; init; } = default!;
    public string? PublishedBy { get; init; }
    public DateTimeOffset PublishedDate { get; init; }
}
