namespace KH.Application.Common.Options;

/// <summary>
/// Binds the <c>SurveyTime</c> configuration section: the local clock a survey's date rules are
/// measured against.
///
/// A fill records calendar values — the day an inspection happened, the time a reading was taken —
/// not instants. The crew reads "today" off a phone set to local time, so a rule such as
/// <c>on_or_before</c> has to be judged against that same day. The server clock is UTC, which is
/// three hours behind Riyadh: between 21:00 and midnight local, UTC still says yesterday, and an
/// answer of today that the fill form accepted would be refused on arrival.
/// </summary>
public sealed class SurveyTimeOptions
{
    public const string SectionName = "SurveyTime";

    /// <summary>
    /// The IANA id is used rather than the Windows one (<c>Arab Standard Time</c>) because it
    /// resolves on both, and this app is deployed to Linux.
    /// </summary>
    public const string DefaultTimeZoneId = "Asia/Riyadh";

    public string TimeZoneId { get; init; } = DefaultTimeZoneId;

    /// <summary>
    /// The configured branch, or UTC when the host does not know the id. Falling back rather than
    /// throwing keeps a misconfigured branch from taking submissions down altogether — the rules
    /// still apply, just against the server's own clock.
    /// </summary>
    public TimeZoneInfo ResolveTimeZone() =>
        TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out var branch) ? branch : TimeZoneInfo.Utc;
}
