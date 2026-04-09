using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Caching.Redis;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaRedisCache(this IServiceCollection services, Action<RedisCacheOptions> configure)
    {
        services.AddOptions<RedisCacheOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<RedisConnectionManager>();
        services.TryAddSingleton<ICache, RedisCache>();
        services.TryAddSingleton<IDistributedLock, RedisDistributedLock>();
        return services;
    }
}
