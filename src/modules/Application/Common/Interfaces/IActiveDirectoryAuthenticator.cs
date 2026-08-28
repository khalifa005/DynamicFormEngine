namespace KH.Application.Common.Interfaces;

/// <summary>
/// Checks a password against corporate Active Directory. Used by field-team sign-in when
/// <c>ActiveDirectory:Enabled</c> is on, in place of the account's local Identity password — AD is
/// then the only password authority, and there is no fall back to the stored hash.
/// </summary>
public interface IActiveDirectoryAuthenticator
{
    /// <summary>Whether <c>ActiveDirectory:Enabled</c> is on. False means callers keep their existing password check.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Binds to the directory as <paramref name="userName"/>. Never throws for an unreachable or
    /// misconfigured directory — that comes back as
    /// <see cref="ActiveDirectoryAuthOutcome.Unavailable"/>, which callers must not treat as a wrong
    /// password.
    /// </summary>
    Task<ActiveDirectoryAuthResult> ValidateCredentialsAsync(
        string userName,
        string password,
        CancellationToken cancellationToken);
}

public enum ActiveDirectoryAuthOutcome
{
    /// <summary>AD is switched off; no check was performed.</summary>
    Skipped = 0,

    /// <summary>The directory accepted the credentials.</summary>
    Valid = 1,

    /// <summary>The directory rejected the credentials. Stop — this is a wrong-credentials answer.</summary>
    Invalid = 2,

    /// <summary>
    /// The directory could not answer: unreachable, timed out, TLS refused, or misconfigured. An
    /// operational fault, not a verdict on the password.
    /// </summary>
    Unavailable = 3,
}

/// <summary>
/// <paramref name="Error"/> carries the operator-facing reason for
/// <see cref="ActiveDirectoryAuthOutcome.Unavailable"/> and is null otherwise. It is for logs, not
/// for the sign-in response.
/// </summary>
public sealed record ActiveDirectoryAuthResult(ActiveDirectoryAuthOutcome Outcome, string? Error = null)
{
    public static readonly ActiveDirectoryAuthResult Skipped = new(ActiveDirectoryAuthOutcome.Skipped);

    public static readonly ActiveDirectoryAuthResult Valid = new(ActiveDirectoryAuthOutcome.Valid);

    public static readonly ActiveDirectoryAuthResult Invalid = new(ActiveDirectoryAuthOutcome.Invalid);

    public static ActiveDirectoryAuthResult Unavailable(string error) =>
        new(ActiveDirectoryAuthOutcome.Unavailable, error);
}
