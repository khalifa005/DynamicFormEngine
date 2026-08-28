using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Lookups.TaskTypes;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetTaskTypesQuery : IRequest<Result<PagedResult<FsmsTaskTypeDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Task types are slow-moving reference data, so the whole table is cached as one entry and
/// filtered/paged in memory. Adds and edits evict <see cref="CacheKeys.Fsms.Lookups.TaskTypes"/>
/// automatically through the lookup cache invalidation interceptor.
/// </summary>
public sealed class GetTaskTypesQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetTaskTypesQuery, Result<PagedResult<FsmsTaskTypeDto>>>
{
    public async Task<Result<PagedResult<FsmsTaskTypeDto>>> Handle(
        GetTaskTypesQuery request, CancellationToken cancellationToken)
    {
        var taskTypes = await cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.TaskTypes,
            LoadAllAsync,
            cacheSettings.Value.ToLookupEntryOptions(),
            cancellationToken);

        IEnumerable<FsmsTaskTypeDto> filtered = taskTypes;

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

        return Result<PagedResult<FsmsTaskTypeDto>>.Success(
            new PagedResult<FsmsTaskTypeDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }

    private async ValueTask<List<FsmsTaskTypeDto>> LoadAllAsync(CancellationToken cancellationToken) =>
        await context.FsmsTaskTypes
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new FsmsTaskTypeDto
            {
                Id = x.Id,
                Code = x.Code,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
}
