namespace KH.Application.Fsms.DataMigration.Common;

/// <summary>
/// The survey code an imported record lands under. Derived from the source and the record's own key
/// rather than generated, which is what makes a re-run safe: the second pass looks the code up,
/// finds the survey the first pass wrote and skips the record instead of importing it twice.
///
/// It also reads as what it is. An operator looking at <c>IMP-FULCRUM-e5095d11-…</c> in the worklist
/// can take the tail straight back to the row in their own export.
/// </summary>
public static class MigrationSurveyCode
{
    private const string Prefix = "IMP";
    private const char Separator = '-';

    /// <summary>Matches the <c>SurveyCode</c> column and the create validator's own cap.</summary>
    public const int MaxLength = 60;

    public static string For(string sourceCode, string externalId)
    {
        var code = $"{Prefix}{Separator}{sourceCode.Trim()}{Separator}{externalId.Trim()}";

        // A source with a longer key than Fulcrum's GUID would otherwise fail at the database rather
        // than here. Truncating keeps the prefix — the part that makes the code recognisable — and
        // the leading characters of the key, which are what make it unique in practice.
        return code.Length <= MaxLength ? code : code[..MaxLength];
    }
}
