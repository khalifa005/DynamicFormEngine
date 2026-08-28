namespace Shared.Core.Caching;

/// <summary>
/// Provider-agnostic expiration settings for a single cache entry.
/// Deliberately free of any provider type so slices never reference a caching package.
/// </summary>
public sealed record CacheEntryOptions
{
    /// <summary>Total lifetime of the entry, including the distributed (L2) copy.</summary>
    public TimeSpan Expiration { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Lifetime of the in-process (L1) copy. Kept shorter than <see cref="Expiration"/> so a
    /// node picks up changes made by other nodes reasonably fast once L2 is enabled.
    /// </summary>
    public TimeSpan LocalExpiration { get; init; } = TimeSpan.FromMinutes(5);

    public static CacheEntryOptions FromMinutes(int minutes, int? localMinutes = null)
    {
        var expiration = TimeSpan.FromMinutes(Math.Max(minutes, 1));
        var local = localMinutes.HasValue
            ? TimeSpan.FromMinutes(Math.Max(localMinutes.Value, 1))
            : expiration;

        return new CacheEntryOptions
        {
            Expiration = expiration,
            LocalExpiration = local <= expiration ? local : expiration
        };
    }
}
