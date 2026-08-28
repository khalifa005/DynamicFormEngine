using KH.Domain.Constants.Fsms;

namespace KH.Domain.Entities.Fsms.Surveys;

/// <summary>
/// A survey allocated to a field team. Created by <see cref="Survey.Assign"/> and driven from the
/// parent survey's transitions, so an assignment can never disagree with the survey it belongs to.
/// Table: <c>SURVEY_ASSIGNMENTS</c>.
/// </summary>
public sealed class SurveyAssignment : BaseAuditableEntity<long>
{
    private SurveyAssignment()
    {
    }

    private SurveyAssignment(
        long fieldTeamId,
        string? assignedBy,
        DateTimeOffset assignedDate,
        DateTimeOffset? dueDate,
        string? note)
    {
        FieldTeamId = fieldTeamId;
        AssignedBy = assignedBy;
        AssignedDate = assignedDate;
        DueDate = dueDate;
        Note = note;
        Status = AssignmentStatuses.Pending;
        IsActive = true;
    }

    public long SurveyId { get; private set; }
    public long FieldTeamId { get; private set; }

    /// <summary>See <see cref="AssignmentStatuses"/>.</summary>
    public string Status { get; private set; } = default!;

    public string? AssignedBy { get; private set; }
    public DateTimeOffset AssignedDate { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public DateTimeOffset? StartedDate { get; private set; }
    public DateTimeOffset? SubmittedDate { get; private set; }
    public string? Note { get; private set; }

    internal static SurveyAssignment Create(
        long fieldTeamId,
        string? assignedBy,
        DateTimeOffset assignedDate,
        DateTimeOffset? dueDate,
        string? note)
    {
        if (fieldTeamId <= 0)
        {
            throw new DomainException("An assignment must name the field team it is allocated to.");
        }

        return new SurveyAssignment(
            fieldTeamId,
            assignedBy,
            assignedDate,
            dueDate,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim());
    }

    internal void Start(DateTimeOffset startedDate)
    {
        if (Status != AssignmentStatuses.Pending)
        {
            return;
        }

        Status = AssignmentStatuses.InProgress;
        StartedDate = startedDate;
    }

    internal void Submit(DateTimeOffset submittedDate)
    {
        if (Status is AssignmentStatuses.Approved or AssignmentStatuses.Expired)
        {
            throw new DomainException($"A {Status} assignment cannot be submitted.");
        }

        Status = AssignmentStatuses.Submitted;
        SubmittedDate = submittedDate;
        StartedDate ??= submittedDate;
    }

    internal void Approve()
    {
        Status = AssignmentStatuses.Approved;
        IsActive = false;
    }

    internal void Return() => Status = AssignmentStatuses.Returned;

    /// <summary>Retires the allocation because the survey moved to a different team.</summary>
    internal void Supersede()
    {
        Status = AssignmentStatuses.Reassigned;
        IsActive = false;
    }

    internal void Expire()
    {
        Status = AssignmentStatuses.Expired;
        IsActive = false;
    }
}
