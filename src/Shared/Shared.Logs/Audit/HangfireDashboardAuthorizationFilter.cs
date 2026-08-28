using System.Net.Http.Headers;
using System.Text;
using Hangfire.Annotations;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shared.Core.Options;

namespace Shared.Logs.Audit;

/// <summary>
/// Protects the Hangfire dashboard with HTTP Basic Auth using
/// <see cref="HangfireJobsOptions.DashboardUsername"/> / <see cref="HangfireJobsOptions.DashboardPassword"/>.
/// The browser shows a username/password dialog (SPA JWT is not sent on full-page /hangfire loads).
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private const string BasicScheme = "Basic";
    private const string WwwAuthenticateValue = "Basic realm=\"Hangfire Dashboard\"";

    public bool Authorize([NotNull] DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var env = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        HangfireJobsOptions options = httpContext.RequestServices
            .GetRequiredService<IOptions<HangfireJobsOptions>>()
            .Value;

        // Local Development: open dashboard unless credentials are explicitly configured.
        if (env.IsDevelopment()
            && (string.IsNullOrWhiteSpace(options.DashboardUsername)
                || string.IsNullOrWhiteSpace(options.DashboardPassword)))
        {
            return true;
        }

        if (IsValidBasicAuth(httpContext.Request.Headers.Authorization, options))
        {
            return true;
        }

        httpContext.Response.Headers.WWWAuthenticate = WwwAuthenticateValue;
        return false;
    }

    private static bool IsValidBasicAuth(string? authorizationHeader, HangfireJobsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DashboardUsername)
            || string.IsNullOrWhiteSpace(options.DashboardPassword))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out AuthenticationHeaderValue? header)
            || !BasicScheme.Equals(header.Scheme, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
        }
        catch (FormatException)
        {
            return false;
        }

        int separatorIndex = decoded.IndexOf(':');
        if (separatorIndex < 0)
        {
            return false;
        }

        string username = decoded[..separatorIndex];
        string password = decoded[(separatorIndex + 1)..];

        return string.Equals(username, options.DashboardUsername, StringComparison.Ordinal)
               && string.Equals(password, options.DashboardPassword, StringComparison.Ordinal);
    }
}
