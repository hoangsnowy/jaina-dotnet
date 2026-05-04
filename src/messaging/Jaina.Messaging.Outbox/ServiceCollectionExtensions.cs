using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Messaging.Outbox;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the outbox relay <see cref="OutboxRelay"/> as a hosted service. The caller
    /// must additionally register an <see cref="IOutboxStore"/> implementation (e.g. EF Core
    /// or in-memory) and an <see cref="IOutboxDispatcher"/> that publishes to the broker.
    /// </summary>
    public static IServiceCollection AddJainaOutboxRelay(
        this IServiceCollection services,
        Action<OutboxOptions>? configure = null)
    {
        services.AddOptions<OutboxOptions>();
        if (configure is not null)
            services.Configure(configure);

        services.AddHostedService<OutboxRelay>();
        return services;
    }
}
