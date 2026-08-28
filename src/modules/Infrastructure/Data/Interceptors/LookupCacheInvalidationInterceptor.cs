using KH.Domain.Entities.Fsms.Catalog;
using KH.Domain.Entities.Fsms.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.Core.Caching;
using Shared.Core.Constants;

namespace KH.Infrastructure.Data.Interceptors;

/// <summary>
/// Keeps the FSMS lookup cache honest: any add / edit / delete of a lookup row evicts the cached
/// list for that lookup, so add and edit slices never have to remember to invalidate by hand.
/// Keys are collected while the change tracker still reports the pending states, and evicted only
/// after the save succeeds.
/// </summary>
public sealed class LookupCacheInvalidationInterceptor(ICacheService cache) : SaveChangesInterceptor
{
    private readonly HashSet<string> _pendingKeys = new(StringComparer.Ordinal);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CollectPendingKeys(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        CollectPendingKeys(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (_pendingKeys.Count > 0)
        {
            var keys = TakePendingKeys();
            cache.RemoveAsync(keys).AsTask().GetAwaiter().GetResult();
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (_pendingKeys.Count > 0)
        {
            await cache.RemoveAsync(TakePendingKeys(), cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pendingKeys.Clear();

        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendingKeys.Clear();

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void CollectPendingKeys(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            // A lookup row can back more than one entry: the full list the admin grid pages
            // through, and the slim name map list pages label their rows from. Both are derived
            // from the same table, so both go stale on the same write.
            // Each of the four geography levels also backs the flattened parent chain, which is
            // derived from all of them at once — so a write anywhere in the hierarchy invalidates
            // it, not just the level that changed.
            string[]? keys = entry.Entity switch
            {
                FsmsDepartment => [CacheKeys.Fsms.Lookups.Departments, CacheKeys.Fsms.Lookups.DepartmentNames],
                FsmsCluster => [CacheKeys.Fsms.Lookups.Clusters, CacheKeys.Fsms.Lookups.OrgHierarchy],
                FsmsCbu =>
                [
                    CacheKeys.Fsms.Lookups.Cbus,
                    CacheKeys.Fsms.Lookups.CbuNames,
                    CacheKeys.Fsms.Lookups.OrgHierarchy
                ],
                FsmsBranch =>
                [
                    CacheKeys.Fsms.Lookups.Branches,
                    CacheKeys.Fsms.Lookups.BranchNames,
                    CacheKeys.Fsms.Lookups.OrgHierarchy
                ],
                FsmsOperationArea =>
                [
                    CacheKeys.Fsms.Lookups.OperationAreas,
                    CacheKeys.Fsms.Lookups.OperationAreaNames,
                    CacheKeys.Fsms.Lookups.OrgHierarchy
                ],
                FsmsFaType => [CacheKeys.Fsms.Lookups.FaTypes],
                FsmsTaskType => [CacheKeys.Fsms.Lookups.TaskTypes, CacheKeys.Fsms.Lookups.TaskTypeNames],
                FsmsCustomerType => [CacheKeys.Fsms.Lookups.CustomerTypes, CacheKeys.Fsms.Lookups.CustomerTypeNames],
                FsmsContractor => [CacheKeys.Fsms.Lookups.Contractors],
                FsmsReturnReason => [CacheKeys.Fsms.Lookups.ReturnReasons, CacheKeys.Fsms.Lookups.ReturnReasonNames],
                FieldCatalogEntry => [CacheKeys.Fsms.Catalog.Fields],
                _ => null
            };

            if (keys is not null)
            {
                foreach (var key in keys)
                {
                    _pendingKeys.Add(key);
                }
            }
        }
    }

    private List<string> TakePendingKeys()
    {
        List<string> keys = [.. _pendingKeys];
        _pendingKeys.Clear();

        return keys;
    }
}
