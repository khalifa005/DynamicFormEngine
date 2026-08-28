using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shared.Swagger;

public static class DependencyInjection
{
    public static void AddSwaggerServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSwaggerDocs();
    }

    public static IServiceCollection AddSwaggerDocs(this IServiceCollection services)
    {
        services.AddOptions<SwaggerSetting>()
            .BindConfiguration(SwaggerSetting.SectionName);

        services.AddSwaggerGen();

        services.AddOptions<SwaggerGenOptions>()
            .Configure<IOptions<SwaggerSetting>>((options, swaggerSettingOptions) =>
            {
                var swaggerSetting = swaggerSettingOptions.Value;

                foreach (var version in swaggerSetting.Version)
                {
                    options.SwaggerDoc(version.Version, new OpenApiInfo
                    {
                        Title = swaggerSetting.Title,
                        Version = version.Version,
                        Description = swaggerSetting.Description
                    });
                }

                // Stable, unique operationIds for NSwag Angular clients.
                // Without this, Swashbuckle omits operationId and NSwag falls back to
                // "{lastPathSegment}{HTTP}" (e.g. templatesGET). Collisions across
                // controllers are then suffixed globally as templatesGET2, GET3, …
                options.CustomOperationIds(apiDesc =>
                {
                    if (apiDesc.ActionDescriptor is ControllerActionDescriptor cad)
                    {
                        return $"{cad.ControllerName}_{cad.ActionName}";
                    }

                    return apiDesc.ActionDescriptor.AttributeRouteInfo?.Name;
                });

                options.SchemaFilter<DynamicEnumSchemaFilter>();
                options.DocumentFilter<DynamicEnumDocumentFilter>();

                // Machine-to-machine: X-Api-Key (Internal tools)
                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "X-Api-Key",
                    Description =
                        "API key for machine-to-machine callers. Configure keys under ExternalConsumers."
                });

                // Paste a JWT from POST /api/v1/auth/login
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description =
                        "JWT Bearer token. Prefer Password below, or login via POST /api/v1/auth/login " +
                        "and paste AccessToken here."
                });

                // Username / password → JWT (Swagger Authorize dialog)
                options.AddSecurityDefinition("Password", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Description = "Sign in with FSMS username and password (issues a JWT).",
                    Flows = new OpenApiOAuthFlows
                    {
                        Password = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri("/api/v1/auth/token", UriKind.Relative)
                        }
                    }
                });

                // OR semantics: send ApiKey and/or Bearer (Password flow fills Bearer)
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Password", document)] = []
                });
            });

        return services;
    }

    public static WebApplication UseSwaggerDocsMiddleware(this WebApplication app)
    {
        var swaggerSetting = app.Services.GetRequiredService<IOptions<SwaggerSetting>>().Value;

        if (!swaggerSetting.Enabled)
        {
            app.MapGet("/", () => Results.Content(swaggerSetting.ErrorMessage, "text/plain"));
            app.MapGet("/swagger", () => Results.Content(swaggerSetting.ErrorMessage, "text/plain"));
            return app;
        }

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var version in swaggerSetting.Version)
            {
                var url = version.URL;
                if (!url.StartsWith("/") && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    url = $"/swagger/{url}";
                }
                options.SwaggerEndpoint(url, $"{swaggerSetting.Name} {version.Version}");
            }

            // Enables the Username / Password fields on the Authorize dialog for OAuth2 password flow.
            options.OAuthClientId("swagger-ui");
            options.OAuthAppName(swaggerSetting.Name);

            // Development: UI at / (and /index.html) for local F5.
            // Production: UI at /swagger so Nginx can serve the Angular SPA at /.
            options.RoutePrefix = app.Environment.IsDevelopment() ? string.Empty : "swagger";
        });

        return app;
    }
}
