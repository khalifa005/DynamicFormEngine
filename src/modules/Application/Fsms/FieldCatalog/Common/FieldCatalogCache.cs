using KH.Application.Common.Interfaces;
using KH.Application.Fsms.FieldCatalog.Models;
using Shared.Core.Caching;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.FieldCatalog.Common;

/// <summary>
/// Single read path for the cached field catalog. Both catalog queries go through here so the one
/// cached entry (<see cref="CacheKeys.Fsms.Catalog.Fields"/>) can never be written from two
/// projections that have drifted apart. Adds — including the upsert done by template publish —
/// evict the entry through the cache invalidation interceptor.
/// </summary>
internal static class FieldCatalogCache
{
    public static ValueTask<List<FieldCatalogItemDto>> GetAllAsync(
        IApplicationDbContext context,
        ICacheService cache,
        CacheSettings cacheSettings,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            CacheKeys.Fsms.Catalog.Fields,
            ct => LoadAllAsync(context, ct),
            cacheSettings.ToLookupEntryOptions(),
            cancellationToken);

    private static async ValueTask<List<FieldCatalogItemDto>> LoadAllAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken) =>
        await context.FieldCatalog
            .AsNoTracking()
            .OrderBy(c => c.DataName)
            .Select(c => new FieldCatalogItemDto
            {
                CatalogId = c.Id,
                DataName = c.DataName,
                FieldType = c.FieldType,
                LabelEn = c.LabelEn,
                LabelAr = c.LabelAr,
                Description = c.Description
            })
            .ToListAsync(cancellationToken);
}
