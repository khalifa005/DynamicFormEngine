namespace KH.Application.Auth.Models;

public sealed class AuthTokenDto
{
    public string AccessToken { get; init; } = default!;

    public string RefreshToken { get; init; } = default!;

    public int ExpiresInSeconds { get; init; }

    /// <summary>
    /// Who signed in. A password login could infer this from the form it just submitted, but an SSO
    /// login never sees a form — the identity comes from the assertion — so the server states it.
    /// </summary>
    public string UserName { get; init; } = default!;

    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Permission policies granted to the authenticated user.
    /// </summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>The <c>TEAMS</c> row id behind this login, when it is a field-team account.</summary>
    public long? FieldTeamId { get; init; }

    /// <summary>The crew's own name, so the mobile app can label the session without a second call.</summary>
    public string? FieldTeamName { get; init; }

    /// <summary>
    /// Which owner the <see cref="Scopes"/> rows were read from — <c>Team</c> for a field-team login,
    /// <c>User</c> for a back-office one. See <c>OrgScopeOwnerTypes</c>.
    /// </summary>
    public string? ScopeOwnerType { get; init; }

    /// <summary>
    /// True when the caller is not limited by territory at all — Administrator and Monitor, plus any
    /// account holding no scope rows.
    /// </summary>
    public bool IsUnrestrictedScope { get; init; }

    /// <summary>
    /// The territory the caller works under, straight from <c>ORG_SCOPES</c>. Empty when
    /// <see cref="IsUnrestrictedScope"/> is true.
    /// </summary>
    public IReadOnlyList<AuthScopeDto> Scopes { get; init; } = [];
}
