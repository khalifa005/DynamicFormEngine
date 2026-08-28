namespace KH.Application.Common.Options;

/// <summary>
/// Binds the <c>DatabaseStartup</c> configuration section that controls what the API does to the
/// database when it starts. All flags default to <c>false</c> so a missing section is production-safe;
/// Development appsettings turns them on to preserve local migrate/seed/SQL behaviour.
/// </summary>
public sealed class DatabaseStartupOptions
{
    public const string SectionName = "DatabaseStartup";

    /// <summary>Run EF Core <c>MigrateAsync</c> on API start.</summary>
    public bool ApplyMigrations { get; init; }

    /// <summary>Run roles/permissions/reference/template seed on API start.</summary>
    public bool SeedData { get; init; }

    /// <summary>
    /// Execute <c>CREATE OR ALTER</c> scripts under <c>sql/procedures</c> on API start.
    /// One-off files under <c>sql/scripts</c> are not covered and stay manual.
    /// </summary>
    public bool ApplySqlObjects { get; init; }
}
