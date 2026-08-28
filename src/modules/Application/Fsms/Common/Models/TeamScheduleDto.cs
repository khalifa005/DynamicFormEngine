namespace KH.Application.Fsms.Common.Models;

/// <summary>
/// Shared team work-schedule shape returned by <see cref="Common.Interfaces.ITeamDirectory"/>.
/// </summary>
public sealed class TeamScheduleDto
{
    public long TeamId { get; init; }
    public DateOnly ScheduleDate { get; init; }
    public TimeOnly? WorkStart { get; init; }
    public TimeOnly? WorkEnd { get; init; }
    public string? WorkBranch { get; init; }
    public bool IsActive { get; init; }
}
