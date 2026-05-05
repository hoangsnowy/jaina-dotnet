using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Messaging.Saga;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register a saga and its runner. Caller must additionally provide
    /// <see cref="ISagaRepository{TState}"/> (an in-memory or persistent provider) and
    /// the saga's step types via DI.
    /// </summary>
    public static IServiceCollection AddJainaSaga<TSaga, TState>(this IServiceCollection services)
        where TSaga : Saga<TState>
        where TState : SagaState
    {
        services.AddScoped<TSaga>();
        services.AddScoped<ISagaRunner<TSaga, TState>, SagaRunner<TSaga, TState>>();
        return services;
    }
}
