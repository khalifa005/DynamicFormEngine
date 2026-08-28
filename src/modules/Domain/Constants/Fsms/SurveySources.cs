namespace KH.Domain.Constants.Fsms;

/// <summary>
/// How a survey instance entered the system. <see cref="Api"/> is the generic machine-to-machine
/// inbound feed, <see cref="Team"/> is raised by a field crew from the mobile app, the others are
/// operator-driven.
/// </summary>
public abstract class SurveySources
{
    public const string Api = "API";
    public const string Manual = "MANUAL";
    public const string Import = "IMPORT";
    public const string Team = "TEAM";

    public static readonly IReadOnlyList<string> All =
    [
        Api,
        Manual,
        Import,
        Team
    ];

    public static bool IsDefined(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
