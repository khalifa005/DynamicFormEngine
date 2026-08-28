using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Lookups.Contractors;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetContractorsQuery : IRequest<Result<PagedResult<FsmsContractorDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Contractors are slow-moving reference data, so the whole table is cached as one entry and
/// filtered/paged in memory. Adds and edits evict
/// <see cref="CacheKeys.Fsms.Lookups.Contractors"/> automatically through the lookup cache
/// invalidation interceptor.
/// </summary>
public sealed class GetContractorsQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetContractorsQuery, Result<PagedResult<FsmsContractorDto>>>
{
    public async Task<Result<PagedResult<FsmsContractorDto>>> Handle(
        GetContractorsQuery request, CancellationToken cancellationToken)
    {
        List<FsmsContractorDto> contractors = await cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.Contractors,
            LoadAllAsync,
            cacheSettings.Value.ToLookupEntryOptions(),
            cancellationToken);

        IEnumerable<FsmsContractorDto> filtered = contractors;

        if (request.IsActive.HasValue)
            filtered = filtered.Where(x => x.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            string term = request.SearchTerm.Trim();
            filtered = filtered.Where(x =>
                x.PoNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameAr.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.CommercialRegistration != null &&
                 x.CommercialRegistration.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        List<FsmsContractorDto> matches = filtered.ToList();

        List<FsmsContractorDto> items = matches
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<FsmsContractorDto>>.Success(
            new PagedResult<FsmsContractorDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }

    private async ValueTask<List<FsmsContractorDto>> LoadAllAsync(CancellationToken cancellationToken) =>
        await context.FsmsContractors
            .AsNoTracking()
            .OrderBy(x => x.PoNumber)
            .Select(x => new FsmsContractorDto
            {
                Id = x.Id,
                PoNumber = x.PoNumber,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                CommercialRegistration = x.CommercialRegistration,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
}
