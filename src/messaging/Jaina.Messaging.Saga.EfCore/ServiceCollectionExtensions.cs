using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Messaging.Saga.EfCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register an EF Core saga repository for <typeparamref name="TState"/>. Caller must
    /// register <see cref="IDbContextFactory{TContext}"/> for <typeparamref name="TDbContext"/>
    /// and call <c>modelBuilder.ApplyJainaSaga()</c> from <c>OnModelCreating</c>.
    /// </summary>
    public static IServiceCollection AddJainaEfCoreSagaRepository<TState, TDbContext>(this IServiceCollection services)
        where TState : SagaState
        where TDbContext : DbContext
    {
        services.TryAddSingleton<ISagaRepository<TState>, EfSagaRepository<TState, TDbContext>>();
        return services;
    }
}
