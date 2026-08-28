namespace KH.Domain.Entities.Fsms.Teams;

/// <summary>
/// Links a crew to one of the departments it works for. A team commonly covers several, which the
/// retired comma-separated <c>FieldTeam.Departments</c> column could express but nothing could
/// join or filter on — and filtering is exactly what allocation and the worklist need.
/// Table: <c>TEAM_DEPARTMENTS</c>.
/// </summary>
public sealed class FieldTeamDepartment : BaseEntity<long>
{
    public long TeamId { get; set; }
    public int DepartmentId { get; set; }
}
