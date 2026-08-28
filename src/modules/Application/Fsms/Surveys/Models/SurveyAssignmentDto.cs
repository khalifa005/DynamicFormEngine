namespace KH.Application.Fsms.Surveys.Models;

public sealed class SurveyAssignmentDto
{
    public long AssignmentId { get; init; }
    public long SurveyId { get; init; }
    public long FieldTeamId { get; init; }
    public string? FieldTeamName { get; init; }
    public string Status { get; init; } = default!;
    public string? AssignedBy { get; init; }
    public DateTimeOffset AssignedDate { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? StartedDate { get; init; }
    public DateTimeOffset? SubmittedDate { get; init; }
    public string? Note { get; init; }
    public bool IsActive { get; init; }
}
