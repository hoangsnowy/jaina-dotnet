using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Messaging.RabbitMQ;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaRabbitMQ(this IServiceCollection services, Action<RabbitMQOptions> configure)
    {
        services.AddOptions<RabbitMQOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<RabbitMQConnectionManager>();
        return services;
    }

    public static IServiceCollection AddJainaRabbitMQQueue<T>(this IServiceCollection services, string queueName)
    {
        services.AddSingleton<IQueue<T>>(sp =>
        {
            var manager = sp.GetRequiredService<RabbitMQConnectionManager>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RabbitMQQueue<T>>>();
            return new RabbitMQQueue<T>(manager, logger, queueName);
        });
        return services;
    }
}
