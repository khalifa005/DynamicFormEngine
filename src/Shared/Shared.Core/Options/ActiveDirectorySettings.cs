namespace Shared.Core.Options;

/// <summary>
/// Corporate Active Directory as the password authority for <b>field-team sign-in only</b>
/// (<c>POST /api/v1/fsms/field-team/team-login</c>). Everything here is inert while <see cref="Enabled"/>
/// is false: no LDAP connection is opened and the team password is checked against ASP.NET Identity
/// exactly as it was before AD existed.
///
/// Back-office sign-in is untouched by this section — that door is governed by
/// <see cref="SsoSettings"/>.
/// </summary>
public sealed class ActiveDirectorySettings
{
    public const string SectionName = "ActiveDirectory";

    /// <summary>Default LDAP port when <see cref="Port"/> is left at 0 and <see cref="UseSsl"/> is false.</summary>
    public const int DefaultLdapPort = 389;

    /// <summary>Default LDAPS port when <see cref="Port"/> is left at 0 and <see cref="UseSsl"/> is true.</summary>
    public const int DefaultLdapsPort = 636;

    /// <summary>
    /// The master switch. False means the local Identity password decides a team login, which is what
    /// a developer laptop with no line of sight to a domain controller needs.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The AD domain — <c>HQ.NWC</c> style. Used to build the bind name and, unless
    /// <see cref="Server"/> says otherwise, as the host to connect to: an AD domain name resolves in
    /// DNS to its domain controllers.
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// An explicit domain controller host, for when <see cref="Domain"/> is not resolvable from the
    /// API host. Empty means connect to <see cref="Domain"/>.
    /// </summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>0 means derive from <see cref="UseSsl"/>: 636 with, 389 without.</summary>
    public int Port { get; set; }

    /// <summary>
    /// LDAPS. On by default because a simple bind sends the password in the clear otherwise — leave
    /// it on unless the DC only offers 389 plus <see cref="UseStartTls"/>.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// Negotiate TLS on a plain 389 connection before binding. Mutually exclusive with
    /// <see cref="UseSsl"/>; setting both is a configuration error.
    /// </summary>
    public bool UseStartTls { get; set; }

    /// <summary>
    /// Accept a domain controller certificate that does not chain to a trusted root. UAT convenience
    /// only — with this on, the encrypted channel is no longer authenticated.
    /// </summary>
    public bool TrustServerCertificate { get; set; }

    /// <summary>How the typed team code is turned into an LDAP bind name.</summary>
    public ActiveDirectoryUserNameFormat UserNameFormat { get; set; } =
        ActiveDirectoryUserNameFormat.UserPrincipalName;

    /// <summary>
    /// NetBIOS domain for <see cref="ActiveDirectoryUserNameFormat.DownLevel"/>. Empty falls back to
    /// the first label of <see cref="Domain"/>.
    /// </summary>
    public string NetBiosName { get; set; } = string.Empty;

    /// <summary>
    /// How long to wait on the domain controller. A crew is standing in the field waiting for the
    /// app to open, so this stays short: an unreachable DC should fail fast, not hang the request.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;
}

/// <summary>The bind name AD is asked to authenticate.</summary>
public enum ActiveDirectoryUserNameFormat
{
    /// <summary><c>usercode@domain</c>. The AD default and what NWC accounts normally carry.</summary>
    UserPrincipalName = 0,

    /// <summary><c>DOMAIN\usercode</c>, for a forest where the UPN suffix is not the domain name.</summary>
    DownLevel = 1,

    /// <summary>The typed code, unchanged — the account's <c>sAMAccountName</c> is already qualified.</summary>
    AsTyped = 2,
}
