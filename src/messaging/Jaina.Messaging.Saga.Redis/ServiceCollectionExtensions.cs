using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Jaina.Messaging.Saga.Redis;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register a Redis-backed saga repository for <typeparamref name="TState"/>. Caller
    /// must register <see cref="IConnectionMultiplexer"/> separately.
    /// </summary>
    public static IServiceCollection AddJainaRedisSagaRepository<TState>(
        this IServiceCollection services,
        string keyPrefix = "jaina:saga:")
        where TState : SagaState
    {
        services.TryAddSingleton<ISagaRepository<TState>>(sp =>
            new RedisSagaRepository<TState>(sp.GetRequiredService<IConnectionMultiplexer>(), keyPrefix));
        return services;
    }
}
