using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Messaging.Outbox.EfCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the EF Core outbox: <see cref="IOutbox"/> writes into the user's
    /// <typeparamref name="TDbContext"/> change tracker, <see cref="IOutboxStore"/> reads via
    /// an <see cref="IDbContextFactory{TContext}"/> for the relay loop.
    /// </summary>
    /// <remarks>
    /// Caller must register <typeparamref name="TDbContext"/> with both
    /// <c>AddDbContext&lt;TDbContext&gt;</c> AND <c>AddDbContextFactory&lt;TDbContext&gt;</c>
    /// (or use <c>AddDbContextFactory</c> alone if no scoped DbContext is needed elsewhere),
    /// and call <c>modelBuilder.ApplyJainaOutbox()</c> from <c>OnModelCreating</c>.
    /// </remarks>
    public static IServiceCollection AddJainaEfCoreOutbox<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.TryAddScoped<IOutbox, EfOutbox<TDbContext>>();
        services.TryAddSingleton<IOutboxStore, EfOutboxStore<TDbContext>>();
        return services;
    }
}
