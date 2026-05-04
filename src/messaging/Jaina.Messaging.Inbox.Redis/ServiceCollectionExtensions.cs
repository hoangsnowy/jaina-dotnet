using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Jaina.Messaging.Inbox.Redis;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the Redis inbox store. Caller must additionally register
    /// <see cref="IConnectionMultiplexer"/>.
    /// </summary>
    public static IServiceCollection AddJainaRedisInbox(
        this IServiceCollection services,
        Action<InboxOptions>? configure = null,
        string keyPrefix = "jaina:inbox:")
    {
        services.AddOptions<InboxOptions>();
        if (configure is not null)
            services.Configure(configure);

        services.TryAddSingleton<IInboxStore>(sp =>
            new RedisInboxStore(sp.GetRequiredService<IConnectionMultiplexer>(), keyPrefix));
        return services;
    }
}
