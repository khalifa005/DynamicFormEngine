using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Caching;
using Shared.Core.Options;

namespace KH.Infrastructure.Caching;

/// <summary>
/// Registers the single caching stack used by the whole application.
/// This is the only place that knows which provider backs <see cref="ICacheService"/>.
/// </summary>
public static class CachingServiceRegistration
{
    public static IServiceCollection AddCachingServices(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CacheSettings.SectionName);
        services.Configure<CacheSettings>(section);

        var settings = section.Get<CacheSettings>() ?? new CacheSettings();

        // L1 — always present, in-process.
        services.AddMemoryCache();

        // L2 — optional. HybridCache picks up any registered IDistributedCache automatically,
        // so switching the whole app to a shared cache happens here and nowhere else.
        if (settings.Provider == CacheProviderType.Distributed)
        {
            if (!string.IsNullOrWhiteSpace(settings.DistributedConnection))
            {
                // Fail fast rather than silently caching per-node: a Redis connection was configured
                // but no Redis cache provider is wired up yet.
                //
                // To enable Redis:
                //   1. Add PackageVersion "Microsoft.Extensions.Caching.StackExchangeRedis" to Directory.Packages.props
                //   2. Add the matching PackageReference to Infrastructure.csproj
                //   3. Replace the throw below with:
                //        services.AddStackExchangeRedisCache(options =>
                //        {
                //            options.Configuration = settings.DistributedConnection;
                //            options.InstanceName = settings.InstanceName;
                //        });
                throw new InvalidOperationException(
                    $"{CacheSettings.SectionName}:{nameof(CacheSettings.DistributedConnection)} is configured but the Redis cache " +
                    $"provider is not referenced. See {nameof(CachingServiceRegistration)} for the steps to enable it, or clear " +
                    $"the connection string to use the in-process distributed cache.");
            }

            // No connection configured — an in-process distributed cache keeps the L2 code path
            // exercised locally without requiring Redis.
            services.AddDistributedMemoryCache();
        }

        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = settings.MaximumPayloadBytes;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(Math.Max(settings.DefaultExpirationMinutes, 1)),
                LocalCacheExpiration = TimeSpan.FromMinutes(Math.Max(settings.DefaultLocalExpirationMinutes, 1))
            };
        });

        services.AddSingleton<ICacheService, HybridCacheService>();

        return services;
    }
}
