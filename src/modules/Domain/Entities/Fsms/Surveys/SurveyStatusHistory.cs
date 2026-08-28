namespace KH.Domain.Entities.Fsms.Surveys;

/// <summary>
/// One entry in a survey's append-only status trail. Rows are written by
/// <see cref="Survey"/> on every transition and are never updated or deleted — the trail is the
/// audit record of how a survey reached its current state. Table: <c>SURVEY_STATUS_HISTORY</c>.
/// </summary>
public sealed class SurveyStatusHistory : BaseEntity<long>
{
    private SurveyStatusHistory()
    {
    }

    private SurveyStatusHistory(
        string? fromStatus,
        string toStatus,
        string? changedBy,
        DateTimeOffset changedDate,
        string? note)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedBy = changedBy;
        ChangedDate = changedDate;
        Note = note;
    }

    public long SurveyId { get; private set; }

    /// <summary>Null on the creation row — the survey had no prior status.</summary>
    public string? FromStatus { get; private set; }

    public string ToStatus { get; private set; } = default!;
    public string? ChangedBy { get; private set; }
    public DateTimeOffset ChangedDate { get; private set; }
    public string? Note { get; private set; }

    /// <summary>Only <see cref="Survey"/> writes the trail; nothing else may append to it.</summary>
    internal static SurveyStatusHistory Create(
        string? fromStatus,
        string toStatus,
        string? changedBy,
        DateTimeOffset changedDate,
        string? note)
    {
        if (string.IsNullOrWhiteSpace(toStatus))
        {
            throw new DomainException("A status history entry must have a target status.");
        }

        return new SurveyStatusHistory(
            fromStatus,
            toStatus,
            changedBy,
            changedDate,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim());
    }
}
