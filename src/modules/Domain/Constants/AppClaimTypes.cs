namespace KH.Domain.Constants;

/// <summary>
/// Application-specific JWT claim types. Kept here so the token generator and every reader agree on
/// the same names.
/// </summary>
public static class AppClaimTypes
{
    /// <summary>The <c>TEAMS</c> row id behind a field-team login.</summary>
    public const string FieldTeamId = "field_team_id";

    /// <summary>The field team's WFM user code — what the crew signs in with.</summary>
    public const string UserCode = "user_code";
}
