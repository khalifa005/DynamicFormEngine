using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.FieldCatalog.Common;
using KH.Application.Fsms.FieldCatalog.Models;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.FieldCatalog.Queries.GetFieldCatalogPaged;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetFieldCatalogPagedQuery : IRequest<Result<PagedResult<FieldCatalogItemDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
}

/// <summary>
/// Serves the lookups admin grid from the same cached catalog list as the autocomplete, then
/// filters and pages in memory.
/// </summary>
public sealed class GetFieldCatalogPagedQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetFieldCatalogPagedQuery, Result<PagedResult<FieldCatalogItemDto>>>
{
    public async Task<Result<PagedResult<FieldCatalogItemDto>>> Handle(
        GetFieldCatalogPagedQuery request, CancellationToken cancellationToken)
    {
        var entries = await FieldCatalogCache.GetAllAsync(context, cache, cacheSettings.Value, cancellationToken);

        IEnumerable<FieldCatalogItemDto> filtered = entries;

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            filtered = filtered.Where(c =>
                c.DataName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (c.LabelEn is not null && c.LabelEn.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (c.LabelAr is not null && c.LabelAr.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (c.Description is not null && c.Description.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var matches = filtered.ToList();

        var items = matches
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<FieldCatalogItemDto>>.Success(
            new PagedResult<FieldCatalogItemDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }
}
