namespace KH.Domain.Constants.Fsms;

/// <summary>
/// What a row in <c>ORG_SCOPES</c> belongs to. One table serves all three because the question
/// asked of each is identical — "which parts of the geography does this cover?" — and splitting it
/// per owner would mean three copies of the same expansion logic.
/// </summary>
public abstract class OrgScopeOwnerTypes
{
    /// <summary>A field crew: the territory it works.</summary>
    public const string Team = nameof(Team);

    /// <summary>A back-office login: the territory whose data it may see.</summary>
    public const string User = nameof(User);

    /// <summary>A survey template: the territory it may be used in.</summary>
    public const string Template = nameof(Template);

    public static readonly IReadOnlyList<string> All =
    [
        Team,
        User,
        Template
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
