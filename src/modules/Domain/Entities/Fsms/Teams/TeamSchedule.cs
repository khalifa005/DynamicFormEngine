namespace KH.Domain.Entities.Fsms.Teams;

/// <summary>
/// Local mirror of WFM <c>FIELDTEAMWORKSCHEDULES_GVT</c>. Populated locally until task-09 wires the
/// live WFM API. Table: <c>TEAM_SCHEDULES</c>.
/// </summary>
public sealed class TeamSchedule : BaseAuditableEntity<long>
{
    public long TeamId { get; set; }
    public DateOnly ScheduleDate { get; set; }
    public TimeOnly? WorkStart { get; set; }
    public TimeOnly? WorkEnd { get; set; }

    /// <summary>Work jurisdiction / branch code the team is scheduled to cover.</summary>
    public string? WorkBranch { get; set; }
}
