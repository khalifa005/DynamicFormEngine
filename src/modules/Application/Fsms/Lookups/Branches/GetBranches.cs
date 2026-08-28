using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Lookups.Branches;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetBranchesQuery : IRequest<Result<PagedResult<FsmsBranchDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>Narrows to the branches under one CBU — drives the cascading dropdown.</summary>
    public string? CbuCode { get; init; }
}

/// <summary>
/// Branches are slow-moving reference data, so the whole table is cached as one entry and
/// filtered/paged in memory. Adds and edits evict <see cref="CacheKeys.Fsms.Lookups.Branches"/>
/// automatically through the lookup cache invalidation interceptor.
/// </summary>
public sealed class GetBranchesQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetBranchesQuery, Result<PagedResult<FsmsBranchDto>>>
{
    public async Task<Result<PagedResult<FsmsBranchDto>>> Handle(
        GetBranchesQuery request, CancellationToken cancellationToken)
    {
        var branches = await cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.Branches,
            LoadAllAsync,
            cacheSettings.Value.ToLookupEntryOptions(),
            cancellationToken);

        IEnumerable<FsmsBranchDto> filtered = branches;

        if (request.IsActive.HasValue)
            filtered = filtered.Where(x => x.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.CbuCode))
        {
            var cbuCode = request.CbuCode.Trim();
            filtered = filtered.Where(x => x.CbuCode != null && x.CbuCode.Equals(cbuCode, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            filtered = filtered.Where(x =>
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameAr.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.CbuCode != null && x.CbuCode.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (x.BranchCode != null && x.BranchCode.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var matches = filtered.ToList();

        var items = matches
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<FsmsBranchDto>>.Success(
            new PagedResult<FsmsBranchDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }

    private async ValueTask<List<FsmsBranchDto>> LoadAllAsync(CancellationToken cancellationToken) =>
        await context.FsmsBranches
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new FsmsBranchDto
            {
                Id = x.Id,
                Code = x.Code,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                TaskZone = x.TaskZone,
                BranchCode = x.BranchCode,
                CbuCode = x.CbuCode,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
}
