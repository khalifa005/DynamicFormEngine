using System.DirectoryServices.Protocols;
using System.Net;
using KH.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Core.Options;

namespace KH.Infrastructure.Identity.ActiveDirectory;

/// <summary>
/// Validates a password against Active Directory with an LDAP simple bind.
///
/// The WFM mobile API this behaviour is copied from uses
/// <c>PrincipalContext.ValidateCredentials</c> (<c>System.DirectoryServices.AccountManagement</c>),
/// which is a Windows-only API and throws on the Linux host this API is deployed to. An LDAP bind is
/// the same question asked over the wire — "does this password authenticate this account" — and runs
/// on both platforms.
/// </summary>
/// <remarks>
/// The Linux host needs OpenLDAP present (<c>libldap-2.5-0</c> / <c>libldap-common</c>) or the very
/// first bind fails to load its native library; that surfaces as
/// <see cref="ActiveDirectoryAuthOutcome.Unavailable"/> with the loader message, not as a rejected
/// password.
/// </remarks>
public sealed class LdapActiveDirectoryAuthenticator(
    IOptions<ActiveDirectorySettings> options,
    ILogger<LdapActiveDirectoryAuthenticator> logger)
    : IActiveDirectoryAuthenticator
{
    /// <summary>LDAP <c>invalidCredentials</c>. Every other result code is an operational fault.</summary>
    private const int InvalidCredentialsResultCode = 49;

    private const string DomainRequired =
        "ActiveDirectory:Domain (or ActiveDirectory:Server) is required when ActiveDirectory:Enabled is true.";

    private const string TransportConflict =
        "ActiveDirectory:UseSsl and ActiveDirectory:UseStartTls cannot both be true.";

    public bool IsEnabled => options.Value.Enabled;

    public async Task<ActiveDirectoryAuthResult> ValidateCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            return ActiveDirectoryAuthResult.Skipped;
        }

        var host = string.IsNullOrWhiteSpace(settings.Server) ? settings.Domain : settings.Server;

        if (string.IsNullOrWhiteSpace(host))
        {
            return ActiveDirectoryAuthResult.Unavailable(DomainRequired);
        }

        if (settings is { UseSsl: true, UseStartTls: true })
        {
            return ActiveDirectoryAuthResult.Unavailable(TransportConflict);
        }

        // An LDAP simple bind with an empty password is an *unauthenticated* bind: the directory
        // answers success without checking anything. Nothing below may run for a blank password.
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(userName))
        {
            return ActiveDirectoryAuthResult.Invalid;
        }

        var bindName = BuildBindName(userName.Trim(), settings);

        // Bind() blocks the calling thread; connection.Timeout is what bounds it. The token only
        // cancels a bind that has not started yet, which is the most a synchronous API allows.
        return await Task.Run(
            () => Bind(host, bindName, password, settings),
            cancellationToken);
    }

    private ActiveDirectoryAuthResult Bind(
        string host,
        string bindName,
        string password,
        ActiveDirectorySettings settings)
    {
        var port = settings.Port > 0
            ? settings.Port
            : settings.UseSsl ? ActiveDirectorySettings.DefaultLdapsPort : ActiveDirectorySettings.DefaultLdapPort;

        try
        {
            using var connection = new LdapConnection(new LdapDirectoryIdentifier(host, port))
            {
                AuthType = AuthType.Basic,
                Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            };

            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.SecureSocketLayer = settings.UseSsl;

            if (settings.TrustServerCertificate)
            {
                connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
            }

            if (settings.UseStartTls)
            {
                connection.SessionOptions.StartTransportLayerSecurity(null);
            }

            connection.Bind(new NetworkCredential(bindName, password));

            logger.LogInformation(
                "Active Directory accepted {BindName} on {Host}:{Port}.",
                bindName,
                host,
                port);

            return ActiveDirectoryAuthResult.Valid;
        }
        catch (LdapException ex) when (ex.ErrorCode == InvalidCredentialsResultCode)
        {
            logger.LogInformation(
                "Active Directory rejected {BindName} on {Host}:{Port}.",
                bindName,
                host,
                port);

            return ActiveDirectoryAuthResult.Invalid;
        }
        catch (Exception ex)
        {
            // Unreachable DC, TLS refused, missing OpenLDAP on the host: none of these say anything
            // about the password, so they must not be reported as wrong credentials.
            logger.LogError(
                ex,
                "Active Directory could not validate {BindName} on {Host}:{Port}.",
                bindName,
                host,
                port);

            return ActiveDirectoryAuthResult.Unavailable(ex.Message);
        }
    }

    private static string BuildBindName(string userName, ActiveDirectorySettings settings) =>
        settings.UserNameFormat switch
        {
            ActiveDirectoryUserNameFormat.DownLevel => $"{ResolveNetBiosName(settings)}\\{userName}",
            ActiveDirectoryUserNameFormat.AsTyped => userName,
            _ => userName.Contains('@', StringComparison.Ordinal)
                ? userName
                : $"{userName}@{settings.Domain}",
        };

    private static string ResolveNetBiosName(ActiveDirectorySettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.NetBiosName))
        {
            return settings.NetBiosName;
        }

        var firstLabel = settings.Domain.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return (firstLabel ?? settings.Domain).ToUpperInvariant();
    }
}
