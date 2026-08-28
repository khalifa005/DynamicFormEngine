using System.Text.Json;
using KH.Application.Fsms.Common.Definition;

namespace KH.Application.Fsms.Submissions.Common;

/// <summary>
/// One answer as it is rolled up onto the survey: the question's own labels, the stored value, and —
/// for a choice field — the option's labels resolved at fill time. Everything a reader needs is here,
/// so the summary can be shown without going back to the template definition.
/// </summary>
internal sealed record SummaryAnswer(
    string Type,
    string? LabelEn,
    string? LabelAr,
    object? Value,
    string? DisplayEn,
    string? DisplayAr);

/// <summary>
/// Condenses one fill into the compact document rolled up onto the survey. Media answers (photo,
/// video, audio, file, signature) and long text are left out — the summary is a scannable digest
/// of the fill, not a second copy of the submission row.
///
/// Shared by the single and bulk submit slices rather than duplicated per slice: this encodes the
/// shape written into <c>ResultSummaryJson</c>, and two copies drifting apart would leave the survey
/// carrying digests a reader cannot parse the same way.
/// </summary>
internal static class SurveyFillSummary
{
    private const int MaxAnswers = 50;
    private const int MaxTextLength = 200;

    /// <summary>Keeps every key camel-cased, so the digest reads the same as the rest of the API.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Build(
        SurveyDefinition definition,
        IReadOnlyDictionary<string, object?> answers,
        long submissionId,
        string filledByRole,
        string? filledBy,
        DateTimeOffset filledAt)
    {
        var fields = new Dictionary<string, SurveyDefinitionField>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.Fields)
        {
            fields[field.DataName] = field;
        }

        var digest = new Dictionary<string, SummaryAnswer>(StringComparer.Ordinal);

        // Keys already match the definition's trimmed data_names — the caller normalizes them (see
        // SurveyAnswerKeys), so the digest names the same fields the submission row wrote.
        foreach (var (key, value) in answers.OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
        {
            if (digest.Count == MaxAnswers)
            {
                break;
            }

            if (!fields.TryGetValue(key, out var field))
            {
                // An `<data_name>_other` companion is free text belonging to its choice field, not an
                // answer of its own — it reads under that field's labels rather than unlabelled.
                field = SurveyChoiceOther.OwnerOf(key, fields);
            }

            if (BuilderElementTypes.IsMedia(field?.FieldType) || !IsDigestible(value, field))
            {
                continue;
            }

            var (displayEn, displayAr) = SurveyAnswerDisplay.Resolve(field, value);

            digest[key] = new SummaryAnswer(
                field?.FieldType ?? string.Empty,
                field?.LabelEn,
                field?.LabelAr,
                value,
                displayEn,
                displayAr);
        }

        return JsonSerializer.Serialize(new
        {
            submissionId,
            filledByRole,
            filledBy,
            filledAt,
            answerCount = answers.Count,
            answers = digest,
        }, SerializerOptions);
    }

    /// <summary>
    /// A choice field's answer may be an array (multi-select); a geolocation answer is a
    /// <c>{ lat, lng }</c> object and a calendar-with-hours answer a <c>{ sat: { from, to }, … }</c> one.
    /// All are kept, since each resolves to a display line above. Everything else has to be a scalar
    /// short enough to read at a glance.
    /// </summary>
    private static bool IsDigestible(object? value, SurveyDefinitionField? field) => value switch
    {
        null => false,
        JsonElement json => json.ValueKind switch
        {
            JsonValueKind.String => (json.GetString()?.Length ?? 0) <= MaxTextLength,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => true,
            JsonValueKind.Array => SurveyAnswerDisplay.IsChoice(field?.FieldType),
            JsonValueKind.Object => SurveyAnswerDisplay.IsGeolocation(field?.FieldType)
                || SurveyAnswerDisplay.IsCalendarWithHours(field?.FieldType),
            _ => false,
        },
        string text => text.Length <= MaxTextLength,
        _ => value is bool or int or long or decimal or double or float or DateTime or DateTimeOffset or DateOnly or TimeOnly,
    };
}
