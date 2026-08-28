using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Lookups.FaTypes;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetFaTypesQuery : IRequest<Result<PagedResult<FsmsFaTypeDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// FA types are slow-moving reference data, so the whole table is cached as one entry and
/// filtered/paged in memory. Adds and edits evict <see cref="CacheKeys.Fsms.Lookups.FaTypes"/>
/// automatically through the lookup cache invalidation interceptor.
/// </summary>
public sealed class GetFaTypesQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetFaTypesQuery, Result<PagedResult<FsmsFaTypeDto>>>
{
    public async Task<Result<PagedResult<FsmsFaTypeDto>>> Handle(
        GetFaTypesQuery request, CancellationToken cancellationToken)
    {
        var faTypes = await cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.FaTypes,
            LoadAllAsync,
            cacheSettings.Value.ToLookupEntryOptions(),
            cancellationToken);

        IEnumerable<FsmsFaTypeDto> filtered = faTypes;

        if (request.IsActive.HasValue)
            filtered = filtered.Where(x => x.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            filtered = filtered.Where(x =>
                x.FaTypeCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameAr.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var matches = filtered.ToList();

        var items = matches
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<FsmsFaTypeDto>>.Success(
            new PagedResult<FsmsFaTypeDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }

    private async ValueTask<List<FsmsFaTypeDto>> LoadAllAsync(CancellationToken cancellationToken) =>
        await context.FsmsFaTypes
            .AsNoTracking()
            .OrderBy(x => x.FaTypeCode)
            .Select(x => new FsmsFaTypeDto
            {
                Id = x.Id,
                FaTypeCode = x.FaTypeCode,
                TaskTypeId = x.TaskTypeId,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
}
