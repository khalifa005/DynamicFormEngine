using KH.Application.Fsms.Common.Org;

namespace KH.Application.Fsms.Users.Models;

/// <summary>Full shape for a single account, including its territory.</summary>
public sealed class UserDetailDto
{
    public string Id { get; init; } = default!;
    public string UserName { get; init; } = default!;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public long? FieldTeamId { get; init; }
    public bool IsEnabled { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// What this account may see, from <c>ORG_SCOPES</c>. Each row pairs a territory with a
    /// department — where they work and what kind of work is theirs — so there is no separate
    /// department list. Empty means everything.
    /// </summary>
    public IReadOnlyList<OrgScopeAssignment> Scopes { get; init; } = [];
}
