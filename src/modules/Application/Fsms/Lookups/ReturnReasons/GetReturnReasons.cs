using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Lookups.ReturnReasons;

/// <summary>
/// The causes a reviewer can pick when returning a survey. Read by the review dialog, so it is
/// available to anyone who can see a survey rather than only to reviewers.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetReturnReasonsQuery : IRequest<Result<PagedResult<FsmsReturnReasonDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Return reasons are slow-moving reference data, so the whole table is cached as one entry and
/// filtered/paged in memory. Adds and edits evict
/// <see cref="CacheKeys.Fsms.Lookups.ReturnReasons"/> automatically through the lookup cache
/// invalidation interceptor.
/// </summary>
public sealed class GetReturnReasonsQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetReturnReasonsQuery, Result<PagedResult<FsmsReturnReasonDto>>>
{
    public async Task<Result<PagedResult<FsmsReturnReasonDto>>> Handle(
        GetReturnReasonsQuery request, CancellationToken cancellationToken)
    {
        var reasons = await cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.ReturnReasons,
            LoadAllAsync,
            cacheSettings.Value.ToLookupEntryOptions(),
            cancellationToken);

        IEnumerable<FsmsReturnReasonDto> filtered = reasons;

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

        return Result<PagedResult<FsmsReturnReasonDto>>.Success(
            new PagedResult<FsmsReturnReasonDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }

    private async ValueTask<List<FsmsReturnReasonDto>> LoadAllAsync(CancellationToken cancellationToken) =>
        await context.FsmsReturnReasons
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new FsmsReturnReasonDto
            {
                Id = x.Id,
                Code = x.Code,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
}
