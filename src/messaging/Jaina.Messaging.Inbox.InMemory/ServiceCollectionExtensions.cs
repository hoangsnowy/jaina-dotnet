using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Messaging.Inbox.InMemory;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register the in-memory inbox store as a singleton. Adds <c>IMemoryCache</c> if not
    /// already registered. Pass a configure action to tweak <see cref="InboxOptions"/>.
    /// </summary>
    public static IServiceCollection AddJainaInMemoryInbox(
        this IServiceCollection services,
        Action<InboxOptions>? configure = null)
    {
        services.AddMemoryCache();
        services.AddOptions<InboxOptions>();
        if (configure is not null)
            services.Configure(configure);

        services.TryAddSingleton<IInboxStore, InMemoryInboxStore>();
        return services;
    }
}
