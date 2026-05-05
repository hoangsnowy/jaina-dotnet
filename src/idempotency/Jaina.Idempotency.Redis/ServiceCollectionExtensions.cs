using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Jaina.Idempotency.Redis;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the Redis idempotency store. Caller must additionally register
    /// <see cref="IConnectionMultiplexer"/> (e.g. via <c>AddSingleton</c> with the connection
    /// string from configuration).
    /// </summary>
    public static IServiceCollection AddJainaRedisIdempotency(
        this IServiceCollection services,
        Action<IdempotencyOptions>? configure = null,
        string keyPrefix = "jaina:idem:")
    {
        services.AddOptions<IdempotencyOptions>();
        if (configure is not null)
            services.Configure(configure);

        services.TryAddSingleton<IIdempotencyStore>(sp =>
            new RedisIdempotencyStore(sp.GetRequiredService<IConnectionMultiplexer>(), keyPrefix));
        return services;
    }
}
