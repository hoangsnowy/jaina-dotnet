using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Messaging.Saga.InMemory;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register an in-memory <see cref="ISagaRepository{TState}"/> for the given state type.
    /// </summary>
    public static IServiceCollection AddJainaInMemorySagaRepository<TState>(this IServiceCollection services)
        where TState : SagaState
    {
        services.TryAddSingleton<ISagaRepository<TState>, InMemorySagaRepository<TState>>();
        return services;
    }
}
