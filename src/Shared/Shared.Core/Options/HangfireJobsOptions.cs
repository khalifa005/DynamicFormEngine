namespace Shared.Core.Options;

public sealed class HangfireJobsOptions
{
    public const string SectionName = "Hangfire";

    /// <summary>When false, Hangfire server and storage are not registered (HTTP audit logging cannot enqueue).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Use in-memory job storage (single process; not for multi-node production).</summary>
    public bool UseMemoryStorage { get; set; } = true;

    public bool EnableDashboard { get; set; } = true;

    public string DashboardPath { get; set; } = "/hangfire";

    /// <summary>When true, Hangfire dashboard is only available in the Development environment.</summary>
    public bool DashboardDevelopmentOnly { get; set; } = true;

    /// <summary>HTTP Basic Auth username for the Hangfire dashboard (browser login prompt).</summary>
    public string DashboardUsername { get; set; } = "hangfire";

    /// <summary>HTTP Basic Auth password for the Hangfire dashboard. Override via env / secrets in Production.</summary>
    public string DashboardPassword { get; set; } = string.Empty;
}
