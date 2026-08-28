using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.FieldCatalog.Common;
using KH.Application.Fsms.FieldCatalog.Models;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.FieldCatalog.Queries.GetFieldCatalog;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetFieldCatalogQuery : IRequest<Result<IReadOnlyList<FieldCatalogItemDto>>>
{
    /// <summary>Optional case-insensitive filter on data name / label.</summary>
    public string? Search { get; init; }

    /// <summary>Max rows to return (autocomplete).</summary>
    public int Take { get; init; } = 20;
}

/// <summary>
/// Backs the form-builder autocomplete, so it runs on every keystroke. The catalog is slow-moving
/// reference data, so the whole table is cached as one entry and filtered in memory.
/// </summary>
public sealed class GetFieldCatalogQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetFieldCatalogQuery, Result<IReadOnlyList<FieldCatalogItemDto>>>
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;

    public async Task<Result<IReadOnlyList<FieldCatalogItemDto>>> Handle(GetFieldCatalogQuery request, CancellationToken cancellationToken)
    {
        var take = request.Take is > 0 and <= MaxTake ? request.Take : DefaultTake;

        var entries = await FieldCatalogCache.GetAllAsync(context, cache, cacheSettings.Value, cancellationToken);

        IEnumerable<FieldCatalogItemDto> filtered = entries;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            filtered = filtered.Where(c =>
                c.DataName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (c.LabelEn is not null && c.LabelEn.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (c.LabelAr is not null && c.LabelAr.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        IReadOnlyList<FieldCatalogItemDto> result = filtered.Take(take).ToList();

        return Result<IReadOnlyList<FieldCatalogItemDto>>.Success(result);
    }
}
