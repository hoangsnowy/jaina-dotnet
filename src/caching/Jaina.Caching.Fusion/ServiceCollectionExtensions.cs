using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Jaina.Caching.Fusion;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaFusionCache(this IServiceCollection services, Action<FusionCacheOptions>? configure = null)
    {
        var options = new FusionCacheOptions();
        configure?.Invoke(options);

        var builder = services.AddFusionCache()
            .WithDefaultEntryOptions(new FusionCacheEntryOptions { Duration = options.DefaultDuration })
            .WithSerializer(new FusionCacheSystemTextJsonSerializer());

        if (!string.IsNullOrEmpty(options.RedisConnectionString))
        {
            services.AddStackExchangeRedisCache(o => o.Configuration = options.RedisConnectionString);
            builder.WithDistributedCache(
                sp => sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
                new FusionCacheSystemTextJsonSerializer());
        }

        services.TryAddSingleton<ICache, FusionCacheAdapter>();
        services.TryAddSingleton<IDistributedLock, FusionCacheAdapter>();
        return services;
    }
}
