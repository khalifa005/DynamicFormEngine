namespace KH.Application.Fsms.Common.Options;

/// <summary>
/// Selects which backing source each team-directory concern resolves to. Bound from the
/// <c>Fsms:TeamIntegration</c> configuration section.
/// </summary>
public sealed class TeamIntegrationOptions
{
    public const string SectionName = "Fsms:TeamIntegration";

    public TeamSource IdentitySource { get; init; } = TeamSource.Local;
    public TeamSource ScheduleSource { get; init; } = TeamSource.Local;
    public TeamSource BranchAssignmentSource { get; init; } = TeamSource.Local;
}

public enum TeamSource
{
    Local,
    Api
}
