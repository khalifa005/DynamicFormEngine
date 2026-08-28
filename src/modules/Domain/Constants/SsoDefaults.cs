namespace KH.Domain.Constants;

/// <summary>
/// Fixed names and paths the SAML sign-on flow is built from. Kept in one place so the scheme names
/// agreed at registration, the paths the Angular app routes to, and the reason codes it renders
/// messages for cannot drift apart.
/// </summary>
public abstract class SsoDefaults
{
    /// <summary>Authentication scheme of the SAML2 handler.</summary>
    public const string Saml2Scheme = "Saml2";

    /// <summary>
    /// Short-lived cookie scheme the SAML handler signs into. It exists only for the single hop
    /// between the assertion landing at the ACS and our callback reading it, and is signed out again
    /// as soon as the callback has what it needs.
    /// </summary>
    public const string TempCookieScheme = "Saml2.Temp";

    /// <summary>Cookie name for <see cref="TempCookieScheme"/>.</summary>
    public const string TempCookieName = "fsms.saml";

    /// <summary>Angular route that exchanges the authorization code for a session.</summary>
    public const string ClientCallbackPath = "/auth/saml-callback";

    /// <summary>Angular route shown when OAM authenticated someone FSMS will not let in.</summary>
    public const string ClientAccessDeniedPath = "/auth/access-denied";

    /// <summary>Where the browser lands after the SAML handler has validated the assertion.</summary>
    public const string CallbackPath = "/api/v1/auth/sso/callback";

    /// <summary>
    /// The controller's own route prefix. The SAML handler's <c>ModulePath</c> must not shadow it —
    /// see <c>SsoAuthenticationSetup</c>, which refuses to start if it does.
    /// </summary>
    public const string ControllerPath = "/api/v1/auth/sso";

    /// <summary>
    /// Why a sign-in that succeeded at OAM was refused here. The Angular access-denied page renders
    /// one localized message per value.
    /// </summary>
    public abstract class DenialReasons
    {
        /// <summary>OAM knows the user; FSMS has no account with that user name.</summary>
        public const string NotProvisioned = "notProvisioned";

        /// <summary>The account exists but is locked out.</summary>
        public const string Inactive = "inactive";

        /// <summary>The account holds no active territory row, so it could not work anyway.</summary>
        public const string NoScope = "noScope";

        /// <summary>A field-crew account tried the back-office door. Crews sign in on the mobile app.</summary>
        public const string CrewAccount = "crewAccount";

        /// <summary>The authorization code was missing, expired, or already used.</summary>
        public const string ExchangeFailed = "exchangeFailed";

        /// <summary>The assertion carried no usable NameID.</summary>
        public const string NoNameId = "noNameId";
    }
}
