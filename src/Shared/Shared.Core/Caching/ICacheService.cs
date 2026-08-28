namespace Shared.Core.Caching;

/// <summary>
/// Cross-cutting cache abstraction. The registered implementation decides whether values live
/// in-process only or also in a distributed store, so callers never change when the provider does.
/// Always build keys from <see cref="Constants.CacheKeys"/> — never inline strings.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, invoking <paramref name="factory"/>
    /// once when the entry is missing. Concurrent callers for the same key share one factory call.
    /// </summary>
    ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Writes (or overwrites) a cache entry.</summary>
    ValueTask SetAsync<T>(
        string key,
        T value,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Evicts a single entry. Safe to call for keys that are not cached.</summary>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Evicts several entries in one round trip.</summary>
    ValueTask RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
}
