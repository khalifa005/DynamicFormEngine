using KH.Domain.Constants.Fsms;

namespace KH.Domain.Entities.Fsms.Templates;

/// <summary>
/// An immutable snapshot of a template's assembled definition, frozen at publish time. One row per
/// target client per publish. Table: <c>TEMPLATE_VERSIONS</c>.
/// </summary>
public sealed class TemplateVersion : BaseEntity<long>
{
    private TemplateVersion()
    {
    }

    private TemplateVersion(
        int versionNo,
        string targetClient,
        string schemaJson,
        string templateSnapshotJson,
        string? publishedBy,
        DateTimeOffset publishedDate)
    {
        VersionNo = versionNo;
        TargetClient = targetClient;
        SchemaJson = schemaJson;
        TemplateSnapshotJson = templateSnapshotJson;
        PublishedBy = publishedBy;
        PublishedDate = publishedDate;
    }

    public long TemplateId { get; private set; }
    public int VersionNo { get; private set; }
    public string TargetClient { get; private set; } = default!;
    public string SchemaJson { get; private set; } = default!;
    public string TemplateSnapshotJson { get; private set; } = default!;
    public string? PublishedBy { get; private set; }
    public DateTimeOffset PublishedDate { get; private set; }

    internal static TemplateVersion Create(
        int versionNo,
        string targetClient,
        string schemaJson,
        string templateSnapshotJson,
        string? publishedBy,
        DateTimeOffset publishedDate)
    {
        if (!TargetClients.IsDefined(targetClient))
        {
            throw new DomainException($"Unknown target client '{targetClient}'.");
        }

        return new TemplateVersion(
            versionNo,
            targetClient,
            schemaJson ?? "{}",
            templateSnapshotJson ?? "{}",
            publishedBy,
            publishedDate);
    }
}
