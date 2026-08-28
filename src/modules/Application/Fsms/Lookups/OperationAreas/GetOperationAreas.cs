using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Lookups.OperationAreas;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetOperationAreasQuery : IRequest<Result<PagedResult<FsmsOperationAreaDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>Narrows to the operation areas under one CBU — drives the cascading dropdown.</summary>
    public string? CbuCode { get; init; }
}

/// <summary>
/// Operation areas are slow-moving reference data, so the whole table is cached as one entry and
/// filtered/paged in memory, the same shape as <see cref="Branches.GetBranchesQuery"/>.
/// </summary>
public sealed class GetOperationAreasQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetOperationAreasQuery, Result<PagedResult<FsmsOperationAreaDto>>>
{
    public async Task<Result<PagedResult<FsmsOperationAreaDto>>> Handle(
        GetOperationAreasQuery request, CancellationToken cancellationToken)
    {
        var areas = await cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.OperationAreas,
            LoadAllAsync,
            cacheSettings.Value.ToLookupEntryOptions(),
            cancellationToken);

        IEnumerable<FsmsOperationAreaDto> filtered = areas;

        if (request.IsActive.HasValue)
            filtered = filtered.Where(x => x.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.CbuCode))
        {
            var cbuCode = request.CbuCode.Trim();
            filtered = filtered.Where(x => x.CbuCode.Equals(cbuCode, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            filtered = filtered.Where(x =>
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameAr.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.MainAreaCode != null && x.MainAreaCode.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var matches = filtered.ToList();

        var items = matches
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<FsmsOperationAreaDto>>.Success(
            new PagedResult<FsmsOperationAreaDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }

    private async ValueTask<List<FsmsOperationAreaDto>> LoadAllAsync(CancellationToken cancellationToken) =>
        await context.FsmsOperationAreas
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new FsmsOperationAreaDto
            {
                Id = x.Id,
                Code = x.Code,
                CbuCode = x.CbuCode,
                MainAreaCode = x.MainAreaCode,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
}
