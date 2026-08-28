using KH.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using MK.FormEngine.API.Services;
using Shared.Core.Filters;
using Shared.Core.Interfaces;
using Shared.Core.Options;
using Shared.Core.Services;

namespace MK.FormEngine.API;

public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<ExternalConsumersOptions>()
            .BindConfiguration(ExternalConsumersOptions.SectionName)
            .PostConfigure(options => options.RebuildLookup());

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUser, Services.CurrentUser>();
        builder.Services.AddScoped<IExternalConsumerContext, ExternalConsumerContext>();
        builder.Services.AddScoped<IExternalConsumerAppDirectory, ExternalConsumerAppDirectory>();
        builder.Services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        builder.Services.AddLocalization();

        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<GlobalExceptionHandlingFilter>();
        });
    }
}
