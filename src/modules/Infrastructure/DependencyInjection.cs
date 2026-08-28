using KH.Application.Common.Interfaces;
using KH.Application.Common.Options;
using KH.Infrastructure.Authorization;
using KH.Infrastructure.Services.Storage;
using KH.Infrastructure.Caching;
using KH.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using KH.Infrastructure.Data.Interceptors;
using KH.Infrastructure.Identity;
using KH.Infrastructure.Identity.ActiveDirectory;
using KH.Infrastructure.Identity.Sso;
using KH.Infrastructure.Services;
using KH.Infrastructure.Services.Fsms;
using KH.Infrastructure.Services.Jobs;
using Shared.Core.Options;
using Shared.Logs.Audit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        Guard.Against.Null(connectionString, message: "Connection string 'DefaultConnection' not found.");

        builder.Services.AddCachingServices(builder.Configuration);

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, LookupCacheInvalidationInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

            // No EnableRetryOnFailure: a transient fault surfaces to the caller rather than being
            // retried underneath it. Turning it back on means every handler that opens its own
            // transaction must drive it through Database.CreateExecutionStrategy() — the retrying
            // strategy refuses a transaction it did not open, because it cannot replay half of one —
            // and each such operation must be safe to run twice. See BulkSubmitSurveys.
            options.UseSqlServer(connectionString);
        });

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.Configure<DatabaseStartupOptions>(
            builder.Configuration.GetSection(DatabaseStartupOptions.SectionName));

        // The local clock a survey's date rules are judged against — see SurveyTimeOptions.
        builder.Services.Configure<SurveyTimeOptions>(
            builder.Configuration.GetSection(SurveyTimeOptions.SectionName));

        // How much of a template's field rules a fill has to satisfy — see SurveyValidationOptions.
        builder.Services.Configure<SurveyValidationOptions>(
            builder.Configuration.GetSection(SurveyValidationOptions.SectionName));

        // File storage for uploaded survey media (local disk today; swappable behind IFileStorage).
        builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.SectionName));
        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

        // Where the migration archive sits under the storage root — see DataMigrationOptions.
        builder.Services.Configure<DataMigrationOptions>(
            builder.Configuration.GetSection(DataMigrationOptions.SectionName));

        builder.Services.AddFsmsServices(builder.Configuration);

        builder.Services
            .AddDefaultIdentity<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();
        builder.Services.AddScoped<IUserAccountService, UserAccountService>();
        builder.Services.AddScoped<IRoleLookup, RoleLookup>();
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<IPermissionResolver, PermissionResolver>();
        builder.Services.AddScoped<IAuthService, AuthService>();

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

        // Bound whether or not SSO is on: AuthService reads it to decide if the password door is
        // still open, and the sign-in page asks for it before drawing itself.
        builder.Services.Configure<SsoSettings>(builder.Configuration.GetSection(SsoSettings.SectionName));

        // Corporate AD as the password authority for field-team sign-in. Registered unconditionally;
        // the authenticator answers "skipped" and opens no connection while ActiveDirectory:Enabled
        // is false, which is what a developer laptop with no domain controller in reach needs.
        builder.Services.Configure<ActiveDirectorySettings>(
            builder.Configuration.GetSection(ActiveDirectorySettings.SectionName));
        builder.Services.AddSingleton<IActiveDirectoryAuthenticator, LdapActiveDirectoryAuthenticator>();

        if (!string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
        {
            builder.Services.AddAuthentication(options =>
                {
                    // Bearer stays the default for both authenticate and challenge, so an unauthorized
                    // API call still answers with a 401 rather than being redirected to the identity
                    // provider. SAML is challenged explicitly by scheme name from the SSO login endpoint.
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidAudience = jwtSettings.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey))
                    };
                })
                .AddSaml2Sso(builder.Configuration, builder.Environment);
        }
    }
}
