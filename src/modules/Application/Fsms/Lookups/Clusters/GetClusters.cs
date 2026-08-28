using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Lookups.Clusters;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetClustersQuery : IRequest<Result<PagedResult<FsmsClusterDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Clusters are the broadest — and slowest-moving — level of the org geography, so the whole table
/// is cached as one entry and filtered/paged in memory, the same shape as
/// <see cref="Branches.GetBranchesQuery"/>.
/// </summary>
public sealed class GetClustersQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetClustersQuery, Result<PagedResult<FsmsClusterDto>>>
{
    public async Task<Result<PagedResult<FsmsClusterDto>>> Handle(
        GetClustersQuery request, CancellationToken cancellationToken)
    {
        var clusters = await cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.Clusters,
            LoadAllAsync,
            cacheSettings.Value.ToLookupEntryOptions(),
            cancellationToken);

        IEnumerable<FsmsClusterDto> filtered = clusters;

        if (request.IsActive.HasValue)
            filtered = filtered.Where(x => x.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            filtered = filtered.Where(x =>
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameAr.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var matches = filtered.ToList();

        var items = matches
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<FsmsClusterDto>>.Success(
            new PagedResult<FsmsClusterDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }

    private async ValueTask<List<FsmsClusterDto>> LoadAllAsync(CancellationToken cancellationToken) =>
        await context.FsmsClusters
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new FsmsClusterDto
            {
                Id = x.Id,
                Code = x.Code,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
}
