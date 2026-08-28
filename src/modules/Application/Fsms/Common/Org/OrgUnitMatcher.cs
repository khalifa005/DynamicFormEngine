using KH.Application.Common.Interfaces;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Options;

namespace KH.Application.Fsms.Common.Org;

/// <summary>
/// Resolves inbound org codes against the cached hierarchy. See <see cref="IOrgUnitMatcher"/> for
/// why a miss is not an error.
/// </summary>
public sealed class OrgUnitMatcher(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IOrgUnitMatcher
{
    public async Task<OrgUnitMatch> MatchAsync(
        string? cbuCode,
        string? branchCode,
        string? operationAreaCode,
        CancellationToken cancellationToken)
    {
        if (IsBlank(cbuCode) && IsBlank(branchCode) && IsBlank(operationAreaCode))
        {
            return OrgUnitMatch.None;
        }

        var hierarchy = await OrgHierarchyCache.GetAsync(context, cache, cacheSettings.Value, cancellationToken);

        var branch = hierarchy.ResolveBranchCode(branchCode);
        var area = hierarchy.ResolveOperationAreaCode(operationAreaCode);
        var cbu = hierarchy.Find(OrgScopeLevels.Cbu, cbuCode)?.CbuCode;

        // The CBU the matched leaves actually sit in beats the one the payload named. Upstream sends
        // both, and when they disagree the leaf is the more specific claim — taking the payload's
        // CBU would file the survey in a CBU that does not contain its own branch.
        var cbuOfBranch = branch is null ? null : hierarchy.Find(OrgScopeLevels.Branch, branch)?.CbuCode;
        var cbuOfArea = area is null ? null : hierarchy.Find(OrgScopeLevels.OperationArea, area)?.CbuCode;

        // Branch and operation area are siblings, so they can legitimately disagree about the CBU
        // only if one of them is wrong. Keeping neither would lose the placement entirely; the
        // branch is the code both WFM and C2M populate most reliably, so it wins.
        var resolvedCbu = cbuOfBranch ?? cbuOfArea ?? cbu;

        // An area belonging to a different CBU than the branch is not this survey's area.
        if (area is not null && cbuOfBranch is not null && !CodesMatch(cbuOfArea, cbuOfBranch))
        {
            area = null;
        }

        var unmatched = new List<string>(3);
        AddIfDropped(unmatched, cbuCode, resolvedCbu is not null);
        AddIfDropped(unmatched, branchCode, branch is not null);
        AddIfDropped(unmatched, operationAreaCode, area is not null);

        return new OrgUnitMatch(resolvedCbu, branch, area, unmatched);
    }

    private static void AddIfDropped(List<string> unmatched, string? sent, bool resolved)
    {
        if (!resolved && !IsBlank(sent))
        {
            unmatched.Add(sent!.Trim());
        }
    }

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    private static bool CodesMatch(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
