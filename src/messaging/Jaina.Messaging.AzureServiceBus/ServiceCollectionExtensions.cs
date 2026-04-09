using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Messaging.AzureServiceBus;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaAzureServiceBus(this IServiceCollection services, Action<ServiceBusOptions> configure)
    {
        services.AddOptions<ServiceBusOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<ServiceBusClientManager>();
        return services;
    }

    public static IServiceCollection AddJainaServiceBusQueue<T>(this IServiceCollection services, string queueName)
    {
        services.AddSingleton<IQueue<T>>(sp =>
        {
            var manager = sp.GetRequiredService<ServiceBusClientManager>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AzureServiceBusQueue<T>>>();
            return new AzureServiceBusQueue<T>(manager, logger, queueName);
        });
        return services;
    }
}
