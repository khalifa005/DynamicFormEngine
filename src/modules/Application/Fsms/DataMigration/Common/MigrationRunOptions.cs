using System.Text.Json;

namespace KH.Application.Fsms.DataMigration.Common;

/// <summary>
/// The choices an operator made when starting a run, persisted on the run row and read back by the
/// job. Every imported survey is placed with these: a survey with no org placement is invisible to
/// the very worklist the person who imported it would look in, so they are not optional.
/// </summary>
public sealed record MigrationRunOptions
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }

    /// <summary>
    /// The crew to attribute the imported work to, when the operator picked one. Allocated before
    /// the fill is recorded — <c>Survey.Assign</c> refuses a survey that already holds submissions.
    /// </summary>
    public long? FieldTeamId { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    /// <summary>
    /// Reads the options back off a run row. A row written before a property existed, or one whose
    /// JSON is unreadable, comes back as defaults rather than taking the run down — the job's own
    /// checks refuse an unplaceable survey a moment later, with a message that says so.
    /// </summary>
    public static MigrationRunOptions FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new MigrationRunOptions();
        }

        try
        {
            return JsonSerializer.Deserialize<MigrationRunOptions>(json, SerializerOptions) ?? new MigrationRunOptions();
        }
        catch (JsonException)
        {
            return new MigrationRunOptions();
        }
    }
}
