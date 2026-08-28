using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Hangfire;
using Hangfire.SqlServer;
using Shared.Logs.Configuration;
using Shared.Logs.Middleware;
using Shared.Logs.Audit;

namespace Shared.Logs;

/// <summary>
/// Central dependency injection extensions for configuring Serilog logging, 
/// correlation tracking, and Hangfire-powered request-response audit logging.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Serilog logging strategy based on builder configuration.
    /// </summary>
    public static void AddSerilogLogging(this IHostApplicationBuilder builder)
    {
        var loggerConfiguration = new LoggerConfiguration();
        SerilogConfiguration.Configure(loggerConfiguration, builder.Configuration);

        Log.Logger = loggerConfiguration.CreateLogger();
        builder.Services.AddSerilog();
    }

    /// <summary>
    /// Registers the tenant logging enrichment middleware.
    /// </summary>
    public static IApplicationBuilder UseTenantLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantLoggingMiddleware>();
    }

    /// <summary>
    /// Registers and configures all request-response audit logging components including:
    /// Audit DbContext, Caller Resolver, Audit Service, and Hangfire background processing.
    /// </summary>
    public static void AddAuditLoggingServices(this IHostApplicationBuilder builder)
    {
        // 1. Bind and register Hangfire job options from appsettings
        var hangfireSection = builder.Configuration.GetSection(Shared.Core.Options.HangfireJobsOptions.SectionName);
        var hangfireOptions = new Shared.Core.Options.HangfireJobsOptions();
        hangfireSection.Bind(hangfireOptions);
        builder.Services.Configure<Shared.Core.Options.HangfireJobsOptions>(hangfireSection);

        // 2. Connection String Resolution
        var connectionString = builder.Configuration.GetConnectionString("AuditDbConnection") 
                               ?? builder.Configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Could not resolve database connection string for Audit Logging. Provide 'AuditDbConnection' or 'DefaultConnection'.");
        }

        // 3. Register Core Audit Services
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICallerResolver, CallerResolver>();
        //builder.Services.AddSingleton<IRequestAuditRecorder, RequestAuditRecorder>();

        // 5. Register Hangfire background services and store jobs in SQL Server (only if enabled in configuration)
        if (hangfireOptions.Enabled)
        {
            builder.Services.AddHangfire((sp, config) =>
            {
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                      .UseSimpleAssemblyNameTypeSerializer()
                      .UseRecommendedSerializerSettings()
                      .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                      {
                          CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                          SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                          QueuePollInterval = TimeSpan.FromSeconds(15),
                          UseRecommendedIsolationLevel = true,
                          DisableGlobalLocks = true // Highly recommended for SqlServer to avoid schema locks and increase throughput
                      });
            });

            // 6. Configure Hangfire background server worker to process 'audit' queue
            builder.Services.AddHangfireServer(options =>
            {
                // Dedicate and isolate queues. The 'audit' queue runs request tracking, 
                // while 'default' remains for other standard background jobs.
                options.Queues = new[] { "audit", "default" };
                options.WorkerCount = Math.Min(Environment.ProcessorCount * 2, 8); // Optimize worker count for high-volume I/O
            });
        }
        else
        {
            Serilog.Log.Information("Hangfire background job processing is disabled via configuration. Audit logs will fall back to direct asynchronous persistence.");
        }
    }

    /// <summary>
    /// Configures the HTTP request pipeline for audit logging:
    /// Registers CorrelationMiddleware and configures the Hangfire Dashboard.
    /// </summary>
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        // 1. Register Correlation Middleware first in the pipeline to track all downstream calls
        app.UseMiddleware<CorrelationMiddleware>();

        // 2. Map Hangfire Dashboard securely if enabled
        var hangfireOptions = app.ApplicationServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Shared.Core.Options.HangfireJobsOptions>>()
            .Value;

        if (hangfireOptions.Enabled && hangfireOptions.EnableDashboard)
        {
            var dashboardPath = string.IsNullOrWhiteSpace(hangfireOptions.DashboardPath) 
                ? "/hangfire" 
                : hangfireOptions.DashboardPath;

            app.UseHangfireDashboard(dashboardPath, new DashboardOptions
            {
                DashboardTitle = "NWC Background Job Dashboard",
                Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
                AppPath = "/swagger" // Redirect back to API documentation when clicking 'Back to site'
            });
        }

        return app;
    }
}
