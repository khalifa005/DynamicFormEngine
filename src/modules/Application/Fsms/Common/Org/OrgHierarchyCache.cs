using System.Text.Json.Serialization;
using KH.Application.Common.Interfaces;
using KH.Domain.Constants.Fsms;
using Shared.Core.Caching;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Common.Org;

/// <summary>
/// The parent chain of every org unit, flattened once so a scope assigned at one level can be
/// expanded to the codes beneath it without walking four tables per request.
///
/// Keys are normalized to <see cref="NormalizeCode"/> for the same reason
/// <c>LookupNameCache</c> normalizes its keys: the cache serializes entries, so a
/// case-insensitive dictionary comparer does not survive the round trip.
/// </summary>
/// <remarks>
/// An operation area's entry carries a null <c>BranchCode</c>, and a branch's a null
/// <c>OperationAreaCode</c>: the two are siblings under the CBU (see <see cref="OrgScopeLevels"/>),
/// so neither can be derived from the other.
/// </remarks>
public sealed record OrgHierarchyEntry(string? ClusterCode, string? CbuCode, string? BranchCode, string? OperationAreaCode);

public static class OrgHierarchyCache
{
    public static ValueTask<OrgHierarchyMaps> GetAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.OrgHierarchy,
            ct => LoadAsync(context, ct),
            cacheSettings.ToLookupEntryOptions(),
            cancellationToken);

    private static async ValueTask<OrgHierarchyMaps> LoadAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var clusters = await context.FsmsClusters
            .AsNoTracking()
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);

        var cbus = await context.FsmsCbus
            .AsNoTracking()
            .Select(x => new { x.Code, x.ClusterCode })
            .ToListAsync(cancellationToken);

        var branches = await context.FsmsBranches
            .AsNoTracking()
            .Select(x => new { x.Code, x.CbuCode, x.BranchCode })
            .ToListAsync(cancellationToken);

        var operationAreas = await context.FsmsOperationAreas
            .AsNoTracking()
            .Select(x => new { x.Code, x.CbuCode, x.MainAreaCode })
            .ToListAsync(cancellationToken);

        var cbuByCode = cbus.ToDictionary(x => NormalizeCode(x.Code), x => x, StringComparer.Ordinal);

        string? ClusterOfCbu(string? cbuCode) =>
            cbuCode is not null && cbuByCode.TryGetValue(NormalizeCode(cbuCode), out var cbu) ? cbu.ClusterCode : null;

        // Keyed by level *and* code. A bare code is not unique across the hierarchy — the operation
        // area table does not even declare its code unique on its own (only per CBU), and a legacy
        // row can leave the same code sitting at two levels at once. Keying by code alone let the
        // last level loaded silently overwrite the earlier one, so a perfectly valid CBU could be
        // rejected because a branch happened to share its code.
        var entries = new Dictionary<string, OrgHierarchyEntry>(StringComparer.Ordinal);

        foreach (var code in clusters)
        {
            entries[EntryKey(OrgScopeLevels.Cluster, code)] = new OrgHierarchyEntry(code, null, null, null);
        }

        foreach (var cbu in cbus)
        {
            entries[EntryKey(OrgScopeLevels.Cbu, cbu.Code)] =
                new OrgHierarchyEntry(cbu.ClusterCode, cbu.Code, null, null);
        }

        foreach (var branch in branches)
        {
            var cbuCode = branch.CbuCode;
            entries[EntryKey(OrgScopeLevels.Branch, branch.Code)] =
                new OrgHierarchyEntry(ClusterOfCbu(cbuCode), cbuCode, branch.Code, null);
        }

        foreach (var area in operationAreas)
        {
            var cbuCode = area.CbuCode;
            entries[EntryKey(OrgScopeLevels.OperationArea, area.Code)] =
                new OrgHierarchyEntry(ClusterOfCbu(cbuCode), cbuCode, null, area.Code);
        }

        var cbuCodesByCluster = cbus
            .GroupBy(x => NormalizeCode(x.ClusterCode))
            .ToDictionary(g => g.Key, g => g.Select(x => x.Code).ToList(), StringComparer.Ordinal);

        var branchCodesByCbu = branches
            .Where(x => x.CbuCode is not null)
            .GroupBy(x => NormalizeCode(x.CbuCode!))
            .ToDictionary(g => g.Key, g => g.Select(x => x.Code).ToList(), StringComparer.Ordinal);

        var areaCodesByCbu = operationAreas
            .GroupBy(x => NormalizeCode(x.CbuCode))
            .ToDictionary(g => g.Key, g => g.Select(x => x.Code).ToList(), StringComparer.Ordinal);

        return new OrgHierarchyMaps(
            entries,
            cbuCodesByCluster,
            branchCodesByCbu,
            areaCodesByCbu,
            BuildAliases(branches.Select(x => (x.Code, x.BranchCode))),
            BuildAliases(operationAreas.Select(x => (x.Code, x.MainAreaCode))));
    }

    /// <summary>
    /// Maps a unit's secondary code onto its canonical <c>Code</c>, so an inbound payload naming a
    /// branch as <c>TC-01</c> or an area as <c>TMA1</c> still resolves. An alias that collides with
    /// a real <c>Code</c>, or with another row's alias, is dropped rather than allowed to win: a
    /// silently wrong scope is worse than an unscoped survey the intake logs as unmatched.
    /// </summary>
    private static Dictionary<string, string> BuildAliases(IEnumerable<(string Code, string? Alias)> rows)
    {
        var pairs = rows.ToList();
        var canonical = pairs.Select(x => NormalizeCode(x.Code)).ToHashSet(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var collisions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (code, alias) in pairs)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            var key = NormalizeCode(alias);

            if (canonical.Contains(key) || !aliases.TryAdd(key, code))
            {
                collisions.Add(key);
            }
        }

        foreach (var key in collisions)
        {
            aliases.Remove(key);
        }

        return aliases;
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    internal static string EntryKey(string level, string code) => $"{level}|{NormalizeCode(code)}";
}

/// <summary>
/// Lookup surface over the flattened hierarchy built by <see cref="OrgHierarchyCache"/>.
///
/// The maps are public properties rather than constructor-captured fields because this type is
/// cached, and the cache round-trips through System.Text.Json: a primary constructor whose
/// parameters bind to nothing public serializes to an empty object and then throws on the way back
/// in. The properties exist for the serializer — callers should use the methods below, which
/// normalize the key for them.
/// </summary>
public sealed class OrgHierarchyMaps
{
    [JsonConstructor]
    public OrgHierarchyMaps(
        Dictionary<string, OrgHierarchyEntry> entriesByCode,
        Dictionary<string, List<string>> cbuCodesByCluster,
        Dictionary<string, List<string>> branchCodesByCbu,
        Dictionary<string, List<string>> areaCodesByCbu,
        Dictionary<string, string> branchCodeAliases,
        Dictionary<string, string> operationAreaAliases)
    {
        EntriesByCode = entriesByCode;
        CbuCodesByCluster = cbuCodesByCluster;
        BranchCodesByCbu = branchCodesByCbu;
        AreaCodesByCbu = areaCodesByCbu;
        BranchCodeAliases = branchCodeAliases;
        OperationAreaAliases = operationAreaAliases;
    }

    /// <summary>
    /// Every org unit keyed by level and normalized code. Serialization surface — prefer
    /// <see cref="Find"/>, which builds the key for callers.
    /// </summary>
    public Dictionary<string, OrgHierarchyEntry> EntriesByCode { get; }

    /// <summary>Serialization surface — prefer <see cref="CbusUnderCluster"/>.</summary>
    public Dictionary<string, List<string>> CbuCodesByCluster { get; }

    /// <summary>Serialization surface — prefer <see cref="BranchesUnderCbu"/>.</summary>
    public Dictionary<string, List<string>> BranchCodesByCbu { get; }

    /// <summary>Serialization surface — prefer <see cref="AreasUnderCbu"/>.</summary>
    public Dictionary<string, List<string>> AreaCodesByCbu { get; }

    /// <summary>Serialization surface — prefer <see cref="ResolveBranchCode"/>.</summary>
    public Dictionary<string, string> BranchCodeAliases { get; }

    /// <summary>Serialization surface — prefer <see cref="ResolveOperationAreaCode"/>.</summary>
    public Dictionary<string, string> OperationAreaAliases { get; }

    /// <summary>
    /// The org unit at <paramref name="level"/> with this code, or null when no such unit exists.
    /// The level is part of the lookup, not a filter applied afterwards: the same code can legally
    /// name a CBU and an operation area at once.
    /// </summary>
    public OrgHierarchyEntry? Find(string level, string? code) =>
        code is not null && EntriesByCode.TryGetValue(OrgHierarchyCache.EntryKey(level, code), out var entry)
            ? entry
            : null;

    public IReadOnlyList<string> CbusUnderCluster(string clusterCode) =>
        CbuCodesByCluster.GetValueOrDefault(Normalize(clusterCode), []);

    public IReadOnlyList<string> BranchesUnderCbu(string cbuCode) =>
        BranchCodesByCbu.GetValueOrDefault(Normalize(cbuCode), []);

    public IReadOnlyList<string> AreasUnderCbu(string cbuCode) =>
        AreaCodesByCbu.GetValueOrDefault(Normalize(cbuCode), []);

    /// <summary>
    /// The canonical branch <c>Code</c> for whatever an upstream system called a branch — its own
    /// <c>Code</c> first, then its <c>BranchCode</c> alias. Null when neither matches.
    /// </summary>
    public string? ResolveBranchCode(string? code) =>
        Resolve(OrgScopeLevels.Branch, code, BranchCodeAliases);

    /// <summary>
    /// The canonical operation-area <c>Code</c>, falling back to the <c>MainAreaCode</c> alias —
    /// C2M sends the maintenance area, not the area code. Null when neither matches.
    /// </summary>
    public string? ResolveOperationAreaCode(string? code) =>
        Resolve(OrgScopeLevels.OperationArea, code, OperationAreaAliases);

    private string? Resolve(string level, string? code, Dictionary<string, string> aliases)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        if (Find(level, code) is { } entry)
        {
            return level == OrgScopeLevels.Branch ? entry.BranchCode : entry.OperationAreaCode;
        }

        return aliases.GetValueOrDefault(Normalize(code));
    }

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
