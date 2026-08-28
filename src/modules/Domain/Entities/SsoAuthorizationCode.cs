namespace KH.Domain.Entities;

/// <summary>
/// A single-use, short-lived code handed to the browser after OAM has authenticated someone, and
/// traded straight back for a real token pair.
///
/// It exists so the access and refresh tokens never ride in a redirect URL, where they would be
/// written to browser history, proxy logs and referrer headers. Only the SHA-256 hash of the code is
/// stored, the same way <see cref="UserRefreshToken"/> holds refresh tokens.
/// </summary>
public sealed class SsoAuthorizationCode
{
    private SsoAuthorizationCode()
    {
    }

    private SsoAuthorizationCode(
        string userId,
        string codeHash,
        string? sessionIndex,
        DateTime expiresAtUtc,
        DateTime createdAtUtc)
    {
        UserId = userId;
        CodeHash = codeHash;
        SessionIndex = sessionIndex;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }

    public string UserId { get; private set; } = default!;

    public string CodeHash { get; private set; } = default!;

    /// <summary>
    /// The IdP's <c>SessionIndex</c> for this sign-in, kept so a future single-logout can name the
    /// session back to OAM. Nothing reads it today.
    /// </summary>
    public string? SessionIndex { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public static SsoAuthorizationCode Create(
        string userId,
        string codeHash,
        string? sessionIndex,
        DateTime expiresAtUtc,
        DateTime createdAtUtc) =>
        new(userId, codeHash, sessionIndex, expiresAtUtc, createdAtUtc);

    public bool IsActive(DateTime utcNow) =>
        ConsumedAtUtc is null && ExpiresAtUtc > utcNow;

    public void Consume(DateTime utcNow) => ConsumedAtUtc = utcNow;
}
