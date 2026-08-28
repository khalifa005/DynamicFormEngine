namespace KH.Domain.Constants.Fsms;

/// <summary>
/// The levels of the organisational geography, mirrored from the WFM reference data:
///
/// <code>
/// Cluster
///   └── Cbu
///        ├── Branch
///        └── OperationArea
/// </code>
///
/// Branch and operation area are <b>siblings</b>, not a chain. An operation area belongs to a CBU
/// directly, so a branch scope does not reach one and only the CBU above covers both. The upstream
/// hierarchy also carries MaintenanceArea and District/City/Area; those are deliberately not
/// modelled here because nothing in FSMS scopes work that finely (the maintenance area survives as
/// a plain attribute on the operation area).
///
/// Because the shape branches, a level's position in <see cref="All"/> says nothing about coverage —
/// <see cref="ParentOf"/> is the only thing that does. <see cref="All"/> exists for
/// <see cref="IsDefined"/> and for presenting the levels in a sensible order.
/// </summary>
public abstract class OrgScopeLevels
{
    public const string Cluster = nameof(Cluster);
    public const string Cbu = nameof(Cbu);
    public const string Branch = nameof(Branch);
    public const string OperationArea = nameof(OperationArea);

    /// <summary>Broadest first, with the two leaf levels last in no meaningful order between them.</summary>
    public static readonly string[] All =
    [
        Cluster,
        Cbu,
        Branch,
        OperationArea
    ];

    /// <summary>
    /// Each level's parent, and the single source of truth for the shape of the geography.
    /// <see cref="Cluster"/> is absent because it has no parent.
    /// </summary>
    private static readonly Dictionary<string, string> Parents = new(StringComparer.Ordinal)
    {
        [Cbu] = Cluster,
        [Branch] = Cbu,
        [OperationArea] = Cbu
    };

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);

    /// <summary>
    /// The level directly above <paramref name="level"/>, or <c>null</c> for <see cref="Cluster"/>
    /// and for anything unrecognised. Callers guard with <see cref="IsDefined"/>.
    /// </summary>
    public static string? ParentOf(string? level) =>
        level is not null && Parents.TryGetValue(level, out var parent) ? parent : null;

    /// <summary>
    /// True when <paramref name="level"/> sits somewhere beneath <paramref name="ancestor"/>.
    /// Two siblings — branch and operation area — answer <c>false</c> in both directions, which is
    /// the whole point of asking rather than comparing positions in <see cref="All"/>.
    /// </summary>
    public static bool IsUnder(string? level, string? ancestor)
    {
        if (level is null || ancestor is null)
        {
            return false;
        }

        for (var current = ParentOf(level); current is not null; current = ParentOf(current))
        {
            if (string.Equals(current, ancestor, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
