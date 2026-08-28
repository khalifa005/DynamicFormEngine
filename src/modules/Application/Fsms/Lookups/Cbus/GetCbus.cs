using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Lookups.Cbus;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetCbusQuery : IRequest<Result<PagedResult<FsmsCbuDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>Narrows to the CBUs under one cluster — drives the cascading dropdown.</summary>
    public string? ClusterCode { get; init; }
}

/// <summary>
/// CBUs are slow-moving reference data, so the whole table is cached as one entry and
/// filtered/paged in memory, the same shape as <see cref="Branches.GetBranchesQuery"/>.
/// </summary>
public sealed class GetCbusQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetCbusQuery, Result<PagedResult<FsmsCbuDto>>>
{
    public async Task<Result<PagedResult<FsmsCbuDto>>> Handle(
        GetCbusQuery request, CancellationToken cancellationToken)
    {
        var cbus = await cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.Cbus,
            LoadAllAsync,
            cacheSettings.Value.ToLookupEntryOptions(),
            cancellationToken);

        IEnumerable<FsmsCbuDto> filtered = cbus;

        if (request.IsActive.HasValue)
            filtered = filtered.Where(x => x.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.ClusterCode))
        {
            var clusterCode = request.ClusterCode.Trim();
            filtered = filtered.Where(x => x.ClusterCode.Equals(clusterCode, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            filtered = filtered.Where(x =>
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameAr.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.OrgCode != null && x.OrgCode.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var matches = filtered.ToList();

        var items = matches
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<FsmsCbuDto>>.Success(
            new PagedResult<FsmsCbuDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }

    private async ValueTask<List<FsmsCbuDto>> LoadAllAsync(CancellationToken cancellationToken) =>
        await context.FsmsCbus
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new FsmsCbuDto
            {
                Id = x.Id,
                Code = x.Code,
                ClusterCode = x.ClusterCode,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                OrgId = x.OrgId,
                OrgCode = x.OrgCode,
                DefaultTaskZone = x.DefaultTaskZone,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
}
