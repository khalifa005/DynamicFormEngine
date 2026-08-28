namespace KH.Application.Common.Models;

/// <summary>
/// Shared paging, search, and sort criteria for lookup list queries.
/// </summary>
public abstract record PaginatedLookupQuery
{
    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public string? SearchTerm { get; init; }

    public bool? IsActive { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }
}
