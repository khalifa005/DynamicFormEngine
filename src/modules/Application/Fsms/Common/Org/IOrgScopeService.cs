using Shared.Core.Common;

namespace KH.Application.Fsms.Common.Org;

/// <summary>
/// Resolves and writes territory assignments for teams, users and templates — the single path
/// every owner goes through so the expansion and validation logic is not reimplemented per owner.
/// </summary>
public interface IOrgScopeService
{
    /// <summary>The expanded territory one owner's stored <c>ORG_SCOPES</c> rows reach.</summary>
    Task<OrgScopeSet> GetScopeAsync(string ownerType, string ownerId, CancellationToken cancellationToken);

    /// <summary>
    /// Batch form of <see cref="GetScopeAsync"/> for owner-keyed scoping (e.g. filtering a page of
    /// Templates by the caller's scope) — one hierarchy load and one <c>ORG_SCOPES</c> read cover
    /// every owner id, rather than one round trip each. An owner id with no rows is simply absent
    /// from the result; the caller decides how to treat "no scope" (see <see cref="OrgScopeSet"/>'s
    /// own "no rows = unrestricted" semantics for the single-owner equivalent).
    /// </summary>
    Task<IReadOnlyDictionary<string, OrgScopeSet>> GetScopesAsync(
        string ownerType,
        IReadOnlyCollection<string> ownerIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The caller's working territory. Back-office accounts use <c>ORG_SCOPES</c> rows on
    /// <see cref="Common.Interfaces.IUser.Id"/>; a field-team login uses the rows on its
    /// <see cref="Common.Interfaces.IUser.FieldTeamId"/> instead. Unrestricted for Administrator
    /// and Monitor regardless of any scope rows on their account.
    /// </summary>
    Task<OrgScopeSet> GetCurrentUserScopeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces every scope row an owner has with the given set. The single write path for teams,
    /// users and templates.
    /// </summary>
    Task<Result<bool>> ReplaceScopesAsync(
        string ownerType,
        string ownerId,
        IReadOnlyList<OrgScopeAssignment> assignments,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rejects an assignment naming a code that does not exist at its level, or one that does exist
    /// but sits under a different parent than the geography actually has.
    /// </summary>
    Task<Result<bool>> ValidateAsync(
        IReadOnlyList<OrgScopeAssignment> assignments,
        CancellationToken cancellationToken);
}
