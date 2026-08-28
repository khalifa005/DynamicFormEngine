using System.Globalization;

namespace KH.Application.Fsms.Surveys.Common;

/// <summary>
/// Builds the human-facing survey code when the caller does not supply one. The inbound API often
/// has no code of its own, and the code is what operators quote, so it has to be readable as well
/// as unique.
/// </summary>
internal static class SurveyCodeFactory
{
    private const string Prefix = "SRV";
    private const string TimestampFormat = "yyMMdd";
    private const int SuffixLength = 4;

    public static string Next(DateTimeOffset now) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}-{now.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture)}-{Guid.NewGuid().ToString("N")[..SuffixLength].ToUpperInvariant()}");
}
