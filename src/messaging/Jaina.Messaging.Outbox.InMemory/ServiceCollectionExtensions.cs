using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Messaging.Outbox.InMemory;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the in-memory <see cref="IOutbox"/> + <see cref="IOutboxStore"/> as singletons.
    /// Pair with <c>AddJainaOutboxRelay()</c> to start the polling loop, and register an
    /// <see cref="IOutboxDispatcher"/> for actual delivery.
    /// </summary>
    public static IServiceCollection AddJainaInMemoryOutbox(this IServiceCollection services)
    {
        services.TryAddSingleton<InMemoryOutboxStore>();
        services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<InMemoryOutboxStore>());
        services.TryAddSingleton<IOutbox, InMemoryOutbox>();
        return services;
    }
}
