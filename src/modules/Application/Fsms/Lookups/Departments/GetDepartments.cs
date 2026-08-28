using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Constants;
using Shared.Core.Options;

namespace KH.Application.Fsms.Lookups.Departments;

[Authorize(Policy = FsmsPolicies.ViewTemplates)]
public record GetDepartmentsQuery : IRequest<Result<PagedResult<FsmsDepartmentDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Departments are small, slow-moving reference data, so the whole table is cached as one entry
/// and filtered/paged in memory. Adds and edits evict <see cref="CacheKeys.Fsms.Lookups.Departments"/>
/// automatically through the lookup cache invalidation interceptor.
/// </summary>
public sealed class GetDepartmentsQueryHandler(
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetDepartmentsQuery, Result<PagedResult<FsmsDepartmentDto>>>
{
    public async Task<Result<PagedResult<FsmsDepartmentDto>>> Handle(
        GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var departments = await cache.GetOrCreateAsync(
            CacheKeys.Fsms.Lookups.Departments,
            LoadAllAsync,
            cacheSettings.Value.ToLookupEntryOptions(),
            cancellationToken);

        IEnumerable<FsmsDepartmentDto> filtered = departments;

        if (request.IsActive.HasValue)
            filtered = filtered.Where(x => x.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            filtered = filtered.Where(x =>
                x.NameEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.NameAr.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var matches = filtered.ToList();

        var items = matches
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<PagedResult<FsmsDepartmentDto>>.Success(
            new PagedResult<FsmsDepartmentDto>(items, matches.Count, request.PageNumber, request.PageSize));
    }

    private async ValueTask<List<FsmsDepartmentDto>> LoadAllAsync(CancellationToken cancellationToken) =>
        await context.FsmsDepartments
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new FsmsDepartmentDto
            {
                Id = x.Id,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
}
