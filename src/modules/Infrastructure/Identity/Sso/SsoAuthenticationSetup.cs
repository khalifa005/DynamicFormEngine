using System.Security.Cryptography.X509Certificates;
using KH.Domain.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Core.Options;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;
using Sustainsys.Saml2.WebSso;

namespace KH.Infrastructure.Identity.Sso;

/// <summary>
/// Registers FSMS as a SAML2 service provider against NWC's Oracle Access Manager federation.
///
/// The assertion is validated by the Sustainsys handler itself — signature, audience, destination and
/// replay — rather than by hand-parsing the XML. Everything the handler establishes is dropped into a
/// short-lived cookie, read once by the callback endpoint, and discarded.
/// </summary>
public static class SsoAuthenticationSetup
{
    /// <summary>
    /// Adds the SAML2 and holding-cookie schemes. A no-op when <c>Sso:Enabled</c> is false — no
    /// scheme is registered and, importantly, no certificate is opened, so an absent or unreadable
    /// PFX cannot stop the host from starting while the feature is off.
    /// </summary>
    public static AuthenticationBuilder AddSaml2Sso(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var settings = configuration.GetSection(SsoSettings.SectionName).Get<SsoSettings>() ?? new SsoSettings();

        if (!settings.Enabled)
        {
            return builder;
        }

        Guard.Against.NullOrWhiteSpace(
            settings.EntityId,
            message: "Sso:EntityId is required when Sso:Enabled is true.");
        Guard.Against.NullOrWhiteSpace(
            settings.IdentityProviderEntityId,
            message: "Sso:IdentityProviderEntityId is required when Sso:Enabled is true.");
        Guard.Against.NullOrWhiteSpace(
            settings.CertPfxFile,
            message: "Sso:CertPfxFile is required when Sso:Enabled is true.");

        // The SAML handler intercepts every request under ModulePath before routing runs and answers
        // anything it does not recognise with an empty 404. Pointed at the controller's own prefix it
        // silently swallows /status, /login, /callback and /exchange — which looks like the endpoints
        // were never registered. Fail loudly at startup instead.
        if (settings.ModulePath.TrimEnd('/')
            .Equals(SsoDefaults.ControllerPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Sso:ModulePath must not be '{SsoDefaults.ControllerPath}' — the SAML handler would " +
                "swallow every AuthSsoController route under it and return 404. Use a sub-path such " +
                $"as '{SsoDefaults.ControllerPath}/saml'.");
        }

        // Holds the SAML principal for the single hop between the assertion landing at the ACS and
        // the callback reading it. Not sliding, and signed out as soon as the callback is done.
        builder.AddCookie(SsoDefaults.TempCookieScheme, options =>
        {
            options.Cookie.Name = SsoDefaults.TempCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

            // The browser arrives at the ACS on a cross-site POST from the IdP, so the cookie written
            // there has to survive that navigation.
            options.Cookie.SameSite = SameSiteMode.None;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = false;
        });

        builder.AddSaml2(SsoDefaults.Saml2Scheme, options =>
        {
            options.SPOptions.EntityId = new EntityId(settings.EntityId);
            options.SPOptions.ModulePath = settings.ModulePath;
            options.SPOptions.ReturnUrl = new Uri(SsoDefaults.CallbackPath, UriKind.Relative);

            // OAM requires signed AuthnRequests and we require signed assertions back.
            options.SPOptions.AuthenticateRequestSigningBehavior = SigningBehavior.Always;
            options.SPOptions.WantAssertionsSigned = true;

            options.SPOptions.ServiceCertificates.Add(new ServiceCertificate
            {
                Certificate = LoadSigningCertificate(settings, environment),
                Use = CertificateUse.Signing,
            });

            options.IdentityProviders.Add(
                new IdentityProvider(new EntityId(settings.IdentityProviderEntityId), options.SPOptions)
                {
                    MetadataLocation = settings.IdentityProviderMetadataUrl,
                    LoadMetadata = true,
                    WantAuthnRequestsSigned = true,

                    // OAM can start a session on its own (IdP-initiated / session sharing between NWC
                    // apps), which arrives without a matching outbound request to correlate against.
                    AllowUnsolicitedAuthnResponse = true,
                });

            // The ACS URL registered with IAM, stamped onto every request we send so the IdP returns
            // the assertion here rather than to another app sharing this service provider.
            if (!string.IsNullOrWhiteSpace(settings.AssertionConsumerServiceUrl))
            {
                options.Notifications.AuthenticationRequestCreated = (request, _, _) =>
                    request.AssertionConsumerServiceUrl = new Uri(settings.AssertionConsumerServiceUrl);
            }

            // Where the validated assertion is deposited for the callback endpoint to pick up.
            // The handler's own correlation cookie is managed by the library, which already writes it
            // as SameSite=None so it survives the round trip out to OAM and back.
            options.SignInScheme = SsoDefaults.TempCookieScheme;
        });

        return builder;
    }

    private static X509Certificate2 LoadSigningCertificate(SsoSettings settings, IHostEnvironment environment)
    {
        var certPath = Path.Combine(environment.ContentRootPath, "CertData", settings.CertPfxFile);

        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException(
                $"SSO signing certificate '{settings.CertPfxFile}' was not found. Place it at '{certPath}', " +
                "or set Sso:Enabled to false.",
                certPath);
        }

        return X509CertificateLoader.LoadPkcs12FromFile(certPath, settings.CertPfxPassword);
    }
}
