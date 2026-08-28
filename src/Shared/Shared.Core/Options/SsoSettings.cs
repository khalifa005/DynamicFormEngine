namespace Shared.Core.Options;

/// <summary>
/// NWC corporate single sign-on (Oracle Access Manager, SAML2). Everything here is inert while
/// <see cref="Enabled"/> is false: no authentication scheme is registered, no certificate is read,
/// and the app authenticates exactly as it did before SSO existed.
/// </summary>
public sealed class SsoSettings
{
    public const string SectionName = "Sso";

    /// <summary>
    /// The master switch. False means no SAML scheme is registered at all — a missing or unreadable
    /// certificate therefore cannot stop the host from starting.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Break-glass: keeps <c>POST /api/v1/auth/login</c> open to <c>Administrator</c> accounts while
    /// SSO is on, so the app is still reachable if OAM is down. Every other back-office account is
    /// turned away and told to use SSO. Field-team sign-in is never affected either way.
    /// </summary>
    public bool AllowLocalLoginForAdministrators { get; set; } = true;

    /// <summary>The service provider entity id registered with NWC IAM.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Path prefix the SAML handler owns, serving <c>{ModulePath}/Acs</c>, <c>/SignIn</c> and
    /// <c>/Logout</c>.
    ///
    /// The handler intercepts <b>every</b> request under this prefix before routing runs, and answers
    /// anything it does not recognise with an empty 404 — so this must never overlap the routes on
    /// <c>AuthSsoController</c> (<c>/status</c>, <c>/login</c>, <c>/callback</c>, <c>/exchange</c>).
    /// It deliberately sits one segment below them.
    /// </summary>
    public string ModulePath { get; set; } = "/api/v1/auth/sso/saml";

    /// <summary>
    /// The absolute ACS URL stamped into outgoing AuthnRequests. Must match, character for character,
    /// what IAM has registered against <see cref="EntityId"/>.
    /// </summary>
    public string AssertionConsumerServiceUrl { get; set; } = string.Empty;

    public string IdentityProviderEntityId { get; set; } = string.Empty;

    /// <summary>Remote metadata URL, or a file name resolved next to the running process.</summary>
    public string IdentityProviderMetadataUrl { get; set; } = string.Empty;

    /// <summary>The OAM session logout URL the browser is sent to on sign-out.</summary>
    public string LogoutUrl { get; set; } = string.Empty;

    /// <summary>PFX file name, resolved under <c>{ContentRoot}/CertData/</c>.</summary>
    public string CertPfxFile { get; set; } = string.Empty;

    /// <summary>
    /// PFX password. Supply through user secrets or the <c>Sso__CertPfxPassword</c> environment
    /// variable — never appsettings.
    /// </summary>
    public string CertPfxPassword { get; set; } = string.Empty;

    /// <summary>Angular origin the callback redirects back to.</summary>
    public string ClientAppBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// How long the single-use authorization code handed to the browser stays valid. Short on
    /// purpose: it only has to survive one redirect and one immediate exchange call.
    /// </summary>
    public int AuthorizationCodeLifetimeSeconds { get; set; } = 60;
}
