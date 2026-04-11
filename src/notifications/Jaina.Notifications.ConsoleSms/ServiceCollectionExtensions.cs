using Jaina.Notifications.ConsoleSms.Sms;
using Jaina.Notifications.Sms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Notifications.ConsoleSms;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a development/test SMS sender that logs messages to <see cref="Microsoft.Extensions.Logging.ILogger"/>
    /// instead of sending real SMS. Intended for local development and testing.
    /// </summary>
    public static IServiceCollection AddJainaConsoleSms(this IServiceCollection services)
    {
        services.TryAddSingleton<ISmsSender, ConsoleSmsLogger>();
        return services;
    }
}
