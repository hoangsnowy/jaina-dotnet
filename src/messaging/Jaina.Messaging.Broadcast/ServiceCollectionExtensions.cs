using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Messaging.Broadcast;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaBroadcast(this IServiceCollection services, Action<BroadcastOptions> configure)
    {
        services.AddOptions<BroadcastOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<BroadcastClient>();
        return services;
    }
}
