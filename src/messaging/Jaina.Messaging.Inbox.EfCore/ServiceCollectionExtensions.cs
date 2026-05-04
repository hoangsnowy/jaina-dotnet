using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Messaging.Inbox.EfCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the EF Core inbox store. Caller must register
    /// <see cref="IDbContextFactory{TContext}"/> for <typeparamref name="TDbContext"/> and
    /// call <c>modelBuilder.ApplyJainaInbox()</c> from <c>OnModelCreating</c>.
    /// </summary>
    public static IServiceCollection AddJainaEfCoreInbox<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddOptions<InboxOptions>();
        services.TryAddSingleton<IInboxStore, EfInboxStore<TDbContext>>();
        return services;
    }
}
