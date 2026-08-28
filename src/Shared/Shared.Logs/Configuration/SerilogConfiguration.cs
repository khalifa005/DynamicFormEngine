using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace Shared.Logs.Configuration;

public static class SerilogConfiguration
{
    /// <summary>
    /// Root used when <c>Logging:BasePath</c> is not configured. Matches the default shipped in
    /// appsettings; on Windows this resolves to the drive root of the running process.
    /// </summary>
    private const string DefaultLogBasePath = "/";

    public static LoggerConfiguration Configure(
        LoggerConfiguration config, IConfiguration appConfig)
    {
        // Where the per-feature log tree is rooted. Configured under Logging:BasePath so it can be
        // pointed at a mounted volume per environment without a rebuild.
        var basePath = appConfig["Logging:BasePath"] ?? DefaultLogBasePath;

        config
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "MK.FormEngine")
            .ReadFrom.Configuration(appConfig);

        // Elasticsearch
        var elasticUri = appConfig["ElasticConfiguration:Uri"];
        if (!string.IsNullOrWhiteSpace(elasticUri))
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            config.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
            {
                AutoRegisterTemplate = true,
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
                IndexFormat = $"mk-form-engine-logs-{environment.ToLower().Replace(".", "-")}-{DateTime.UtcNow:yyyy-MM}",
                NumberOfReplicas = 1,
                NumberOfShards = 2
            });
        }

        // Console
        config.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{TenantId}] {Feature,-20} {Message:lj}{NewLine}{Exception}");

        // Dynamic routing based on the "Feature" property
        config.WriteTo.Map(
            keyPropertyName: "Feature",
            defaultKey: "_global",
            configure: (feature, wt) =>
            {
                var isGlobal = string.IsNullOrWhiteSpace(feature) || 
                               string.Equals(feature, "_global", StringComparison.OrdinalIgnoreCase);

                if (isGlobal)
                {
                    // Global sinks (events with no specific Feature property or explicitly _global)
                    wt.Logger(lc => lc
                        .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Information)
                        .WriteTo.File(
                            path: Path.Combine(basePath, "_global", "Information", "global-info-.log"),
                            rollingInterval: RollingInterval.Day,
                            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{TenantId}] {Message:lj}{NewLine}{Exception}"));

                    wt.Logger(lc => lc
                        .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                        .WriteTo.File(
                            path: Path.Combine(basePath, "_global", "Error", "global-error-.log"),
                            rollingInterval: RollingInterval.Day,
                            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{TenantId}] {Message:lj}{NewLine}{Exception}"));
                }
                else
                {
                    // Per-feature sinks
                    var cleanFeature = feature ?? "unknown";
                    var folder = Path.Combine(basePath, cleanFeature);
                    var name = cleanFeature.ToLowerInvariant();

                    wt.Logger(lc => lc
                        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Verbose || e.Level == LogEventLevel.Debug)
                        .WriteTo.File(
                            path: Path.Combine(folder, "Trace", $"{name}-trace-.log"),
                            rollingInterval: RollingInterval.Day,
                            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{TenantId}] {Message:lj}{NewLine}{Exception}"));

                    wt.Logger(lc => lc
                        .Filter.ByIncludingOnly(e => e.Level == LogEventLevel.Information || e.Level == LogEventLevel.Warning)
                        .WriteTo.File(
                            path: Path.Combine(folder, "Information", $"{name}-info-.log"),
                            rollingInterval: RollingInterval.Day,
                            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{TenantId}] {Message:lj}{NewLine}{Exception}"));

                    wt.Logger(lc => lc
                        .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                        .WriteTo.File(
                            path: Path.Combine(folder, "Error", $"{name}-error-.log"),
                            rollingInterval: RollingInterval.Day,
                            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{TenantId}] {Message:lj}{NewLine}{Exception}"));
                }
            },
            sinkMapCountLimit: 100);

        return config;
    }
}
