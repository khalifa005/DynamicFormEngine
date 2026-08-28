using KH.Application.Common.Interfaces;
using Shared.Core.Caching;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Common.Lookups;

/// <summary>
/// Code/id → name maps for the reference data that list pages display on every row.
///
/// A list page used to resolve these with one correlated subquery per lookup per row, which SQL
/// Server runs as a nested OUTER APPLY — the dominant cost of a paged read even though the data
/// itself is small and changes rarely. Caching the maps whole turns that into a dictionary hit, so
/// a page of rows costs no lookup queries at all.
///
/// Entries are evicted by <c>LookupCacheInvalidationInterceptor</c> whenever a lookup row is
/// written, alongside the full list each map is derived from.
/// </summary>
public static class LookupNameCache
{
    /// <summary>
    /// Branch code → name, keyed by <see cref="NormalizeCode"/>.
    ///
    /// The keys are normalized rather than the dictionary being given a case-insensitive comparer:
    /// the cache serializes entries, and a comparer does not survive that round trip, so relying on
    /// one would quietly become case-sensitive the first time a value came back from the cache
    /// instead of the factory. The previous SQL join matched under the database collation, which is
    /// case-insensitive, so normalizing preserves the behaviour callers already had.
    /// </summary>
    public static ValueTask<Dictionary<string, LookupName>> GetBranchNamesAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.BranchNames,
            ct => LoadBranchNamesAsync(context, ct),
            cacheSettings.ToLookupEntryOptions(),
            cancellationToken);

    /// <summary>CBU code → name, keyed by <see cref="NormalizeCode"/>.</summary>
    public static ValueTask<Dictionary<string, LookupName>> GetCbuNamesAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.CbuNames,
            ct => LoadCbuNamesAsync(context, ct),
            cacheSettings.ToLookupEntryOptions(),
            cancellationToken);

    /// <summary>Operation area code → name, keyed by <see cref="NormalizeCode"/>.</summary>
    public static ValueTask<Dictionary<string, LookupName>> GetOperationAreaNamesAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.OperationAreaNames,
            ct => LoadOperationAreaNamesAsync(context, ct),
            cacheSettings.ToLookupEntryOptions(),
            cancellationToken);

    /// <summary>Department id → name.</summary>
    public static ValueTask<Dictionary<int, LookupName>> GetDepartmentNamesAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.DepartmentNames,
            ct => LoadDepartmentNamesAsync(context, ct),
            cacheSettings.ToLookupEntryOptions(),
            cancellationToken);

    /// <summary>Return-reason code → bilingual name, keyed by <see cref="NormalizeCode"/>.</summary>
    public static ValueTask<Dictionary<string, LookupName>> GetReturnReasonNamesAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.ReturnReasonNames,
            ct => LoadReturnReasonNamesAsync(context, ct),
            cacheSettings.ToLookupEntryOptions(),
            cancellationToken);

    /// <summary>
    /// Resolves a branch name from a map returned by <see cref="GetBranchNamesAsync"/>. Use this rather
    /// than indexing the map directly so the key normalization stays in one place.
    /// </summary>
    public static LookupName? FindBranch(this Dictionary<string, LookupName> branchNames, string? branchCode) =>
        string.IsNullOrWhiteSpace(branchCode)
            ? null
            : branchNames.GetValueOrDefault(NormalizeCode(branchCode));

    /// <summary>Resolves a department name from a map returned by <see cref="GetDepartmentNamesAsync"/>.</summary>
    public static LookupName? FindDepartment(this Dictionary<int, LookupName> departmentNames, int? departmentId) =>
        departmentId is int id ? departmentNames.GetValueOrDefault(id) : null;

    /// <summary>Resolves a CBU name from a map returned by <see cref="GetCbuNamesAsync"/>.</summary>
    public static LookupName? FindCbu(this Dictionary<string, LookupName> cbuNames, string? cbuCode) =>
        string.IsNullOrWhiteSpace(cbuCode) ? null : cbuNames.GetValueOrDefault(NormalizeCode(cbuCode));

    /// <summary>Resolves an operation-area name from a map returned by <see cref="GetOperationAreaNamesAsync"/>.</summary>
    public static LookupName? FindOperationArea(this Dictionary<string, LookupName> operationAreaNames, string? operationAreaCode) =>
        string.IsNullOrWhiteSpace(operationAreaCode) ? null : operationAreaNames.GetValueOrDefault(NormalizeCode(operationAreaCode));

    /// <summary>Resolves a return-reason name from a map returned by <see cref="GetReturnReasonNamesAsync"/>.</summary>
    public static LookupName? FindReturnReason(this Dictionary<string, LookupName> returnReasonNames, string? returnReasonCode) =>
        string.IsNullOrWhiteSpace(returnReasonCode) ? null : returnReasonNames.GetValueOrDefault(NormalizeCode(returnReasonCode));

    /// <summary>Task type id → name.</summary>
    public static ValueTask<Dictionary<long, LookupName>> GetTaskTypeNamesAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.TaskTypeNames,
            ct => LoadTaskTypeNamesAsync(context, ct),
            cacheSettings.ToLookupEntryOptions(),
            cancellationToken);

    /// <summary>Customer type id → name.</summary>
    public static ValueTask<Dictionary<long, LookupName>> GetCustomerTypeNamesAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.CustomerTypeNames,
            ct => LoadCustomerTypeNamesAsync(context, ct),
            cacheSettings.ToLookupEntryOptions(),
            cancellationToken);

    /// <summary>Resolves a task type name from a map returned by <see cref="GetTaskTypeNamesAsync"/>.</summary>
    public static LookupName? FindTaskType(this Dictionary<long, LookupName> taskTypeNames, long? taskTypeId) =>
        taskTypeId is long id ? taskTypeNames.GetValueOrDefault(id) : null;

    /// <summary>Resolves a customer type name from a map returned by <see cref="GetCustomerTypeNamesAsync"/>.</summary>
    public static LookupName? FindCustomerType(this Dictionary<long, LookupName> customerTypeNames, long? customerTypeId) =>
        customerTypeId is long id ? customerTypeNames.GetValueOrDefault(id) : null;

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static async ValueTask<Dictionary<string, LookupName>> LoadBranchNamesAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var branches = await context.FsmsBranches
            .AsNoTracking()
            .Select(x => new { x.Code, x.NameEn, x.NameAr })
            .ToListAsync(cancellationToken);

        var names = new Dictionary<string, LookupName>(branches.Count);

        foreach (var branch in branches)
        {
            // Codes are unique per the branch table's unique index, so a collision here would mean two
            // codes differing only by case. Last one wins rather than throwing — a labelling map is
            // never the right place to fail a worklist read.
            names[NormalizeCode(branch.Code)] = new LookupName(branch.NameEn, branch.NameAr);
        }

        return names;
    }

    private static async ValueTask<Dictionary<string, LookupName>> LoadCbuNamesAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var cbus = await context.FsmsCbus
            .AsNoTracking()
            .Select(x => new { x.Code, x.NameEn, x.NameAr })
            .ToListAsync(cancellationToken);

        var names = new Dictionary<string, LookupName>(cbus.Count);

        foreach (var cbu in cbus)
        {
            names[NormalizeCode(cbu.Code)] = new LookupName(cbu.NameEn, cbu.NameAr);
        }

        return names;
    }

    private static async ValueTask<Dictionary<string, LookupName>> LoadOperationAreaNamesAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var areas = await context.FsmsOperationAreas
            .AsNoTracking()
            .Select(x => new { x.Code, x.NameEn, x.NameAr })
            .ToListAsync(cancellationToken);

        var names = new Dictionary<string, LookupName>(areas.Count);

        foreach (var area in areas)
        {
            names[NormalizeCode(area.Code)] = new LookupName(area.NameEn, area.NameAr);
        }

        return names;
    }

    private static async ValueTask<Dictionary<int, LookupName>> LoadDepartmentNamesAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken) =>
        await context.FsmsDepartments
            .AsNoTracking()
            .Select(x => new { x.Id, x.NameEn, x.NameAr })
            .ToDictionaryAsync(
                x => x.Id,
                x => new LookupName(x.NameEn, x.NameAr),
                cancellationToken);

    private static async ValueTask<Dictionary<string, LookupName>> LoadReturnReasonNamesAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var reasons = await context.FsmsReturnReasons
            .AsNoTracking()
            .Select(x => new { x.Code, x.NameEn, x.NameAr })
            .ToListAsync(cancellationToken);

        var names = new Dictionary<string, LookupName>(reasons.Count);

        foreach (var reason in reasons)
        {
            names[NormalizeCode(reason.Code)] = new LookupName(reason.NameEn, reason.NameAr);
        }

        return names;
    }

    private static async ValueTask<Dictionary<long, LookupName>> LoadTaskTypeNamesAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken) =>
        await context.FsmsTaskTypes
            .AsNoTracking()
            .Select(x => new { x.Id, x.NameEn, x.NameAr })
            .ToDictionaryAsync(
                x => x.Id,
                x => new LookupName(x.NameEn, x.NameAr),
                cancellationToken);

    private static async ValueTask<Dictionary<long, LookupName>> LoadCustomerTypeNamesAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken) =>
        await context.FsmsCustomerTypes
            .AsNoTracking()
            .Select(x => new { x.Id, x.NameEn, x.NameAr })
            .ToDictionaryAsync(
                x => x.Id,
                x => new LookupName(x.NameEn, x.NameAr),
                cancellationToken);
}
