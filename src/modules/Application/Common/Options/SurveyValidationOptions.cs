namespace KH.Application.Common.Options;

/// <summary>
/// Binds the <c>SurveyValidation</c> configuration section: how much of a template's field rules the
/// server enforces when a fill arrives.
/// </summary>
/// <remarks>
/// This exists because turning the rules on is a breaking change to an API that live clients already
/// post to. Every fill must now satisfy every required field, and a survey is answered from both
/// sides — so a back-office amendment that used to carry four answers now has to carry the whole
/// required set. If production traffic turns out to depend on partial fills in a way that was not
/// anticipated, this is the way back out without a redeploy.
/// </remarks>
public sealed class SurveyValidationOptions
{
    public const string SectionName = "SurveyValidation";

    /// <summary>
    /// Whether <c>required</c> and the scalar rules (lengths, pattern, numeric bounds, choice
    /// membership) are enforced on submit.
    /// </summary>
    /// <remarks>
    /// Date rules are enforced regardless of this flag — they shipped before it existed and no
    /// client depends on their being off.
    /// </remarks>
    public bool EnforceFieldRules { get; init; } = true;
}
