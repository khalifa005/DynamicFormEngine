using KH.Application.Fsms.Common.Interfaces;
using KH.Application.Fsms.Common.Options;
using KH.Application.Fsms.Reporting.Interfaces;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Infrastructure.Services.Fsms.Reporting;
using KH.Infrastructure.Services.Fsms.Submissions;
using KH.Infrastructure.Services.Fsms.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KH.Infrastructure.Services.Fsms;

public static class FsmsServiceRegistration
{
    public static IServiceCollection AddFsmsServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TeamIntegrationOptions>(configuration.GetSection(TeamIntegrationOptions.SectionName));

        services.AddScoped<ITeamDirectory, LocalTeamDirectory>();

        services.AddSingleton<SqlTemplateStore>();
        services.AddScoped<ISurveySubmissionStore, SurveySubmissionStore>();

        services.AddScoped<IDashboardStore, DashboardStore>();

        return services;
    }
}
