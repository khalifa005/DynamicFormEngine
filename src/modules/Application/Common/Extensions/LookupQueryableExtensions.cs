using KH.Application.Common.Models;
using Shared.Core.Entities;

namespace KH.Application.Common.Extensions;

public static class LookupQueryableExtensions
{
    public static IQueryable<T> ApplyLookupFilters<T>(this IQueryable<T> query, PaginatedLookupQuery criteria)
        where T : LookupEntity
    {
        if (criteria.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == criteria.IsActive.Value);
        }
        else
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm.Trim();
            query = query.Where(x =>
                x.Code.Contains(term)
                || x.NameEn.Contains(term)
                || x.NameAr.Contains(term)
                || (x.Description != null && x.Description.Contains(term)));
        }

        return query.ApplyLookupSorting(criteria);
    }

    private static IQueryable<T> ApplyLookupSorting<T>(this IQueryable<T> query, PaginatedLookupQuery criteria)
        where T : LookupEntity
    {
        var descending = string.Equals(criteria.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return (criteria.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "code" => descending ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code),
            "nameen" => descending ? query.OrderByDescending(x => x.NameEn) : query.OrderBy(x => x.NameEn),
            "namear" => descending ? query.OrderByDescending(x => x.NameAr) : query.OrderBy(x => x.NameAr),
            _ => descending ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id)
        };
    }
}
