using System.Reflection;
using KH.Application.Common.Behaviours;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.DataMigration.Adapters;
using KH.Application.Fsms.DataMigration.Common;
using KH.Application.Fsms.DataMigration.Jobs;
using KH.Application.Fsms.Surveys.Jobs;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());

        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        builder.Services.AddScoped<IOrgScopeService, OrgScopeService>();

        builder.Services.AddScoped<IOrgUnitMatcher, OrgUnitMatcher>();

        builder.Services.AddScoped<ISurveyVersionMigrationJob, SurveyVersionMigrationJob>();
        builder.Services.AddScoped<IDataMigrationJob, DataMigrationJob>();

        builder.Services.AddSingleton<IMigrationSourceAdapter, FulcrumExcelSourceAdapter>();
        builder.Services.AddSingleton<IMigrationSourceRegistry, MigrationSourceRegistry>();

        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
        });
    }
}
