namespace KH.Application.Fsms.Surveys.Models;

/// <summary>One append-only status change on a survey, oldest first in the timeline query.</summary>
public sealed class SurveyTimelineEntryDto
{
    public long EntryId { get; init; }
    public long SurveyId { get; init; }

    /// <summary>Null on the creation entry — the survey had no prior status.</summary>
    public string? FromStatus { get; init; }

    public string ToStatus { get; init; } = default!;
    public string? ChangedBy { get; init; }

    /// <summary>
    /// The display name behind <see cref="ChangedBy"/>. Null when the stamp names no account or the
    /// account is gone, leaving the reader to fall back to the id.
    /// </summary>
    public string? ChangedByName { get; init; }
    public DateTimeOffset ChangedDate { get; init; }
    public string? Note { get; init; }
}
