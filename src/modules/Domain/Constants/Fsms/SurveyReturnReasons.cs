namespace KH.Domain.Constants.Fsms;

/// <summary>
/// Why a reviewer sent a survey back for rework. Carried alongside the free-text note so the crew
/// sees at a glance what is wrong, and so returns can be reported on by cause rather than by
/// reading a thousand notes.
///
/// These are the seeded codes. The backing <c>LKP_RETURN_REASON</c> table is editable, so a code a
/// support operator adds later validates against the table, not against this list — see
/// <see cref="IsSeeded"/>.
/// </summary>
public abstract class SurveyReturnReasons
{
    /// <summary>Required answers are blank or the form was submitted half-filled.</summary>
    public const string MissingData = "MISSING_DATA";

    /// <summary>The readings were taken at the wrong asset or the wrong address.</summary>
    public const string WrongLocation = "WRONG_LOCATION";

    /// <summary>Attached photos are unreadable, missing or do not show what was asked for.</summary>
    public const string PoorPhoto = "POOR_PHOTO";

    /// <summary>The site has to be visited again before this survey can be closed.</summary>
    public const string NeedsRevisit = "NEEDS_REVISIT";

    /// <summary>The wrong crew was allocated; the work belongs to a different team.</summary>
    public const string WrongTeam = "WRONG_TEAM";

    /// <summary>Anything the codes above do not cover — the note carries the detail.</summary>
    public const string Other = "OTHER";

    public static readonly IReadOnlyList<string> All =
    [
        MissingData,
        WrongLocation,
        PoorPhoto,
        NeedsRevisit,
        WrongTeam,
        Other
    ];

    /// <summary>
    /// Whether the code is one this application shipped. Operator-added codes are not, so callers
    /// validating user input should check the lookup table instead of this.
    /// </summary>
    public static bool IsSeeded(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);

    /// <summary>Longest code the column has to hold.</summary>
    public const int MaxCodeLength = 50;
}
