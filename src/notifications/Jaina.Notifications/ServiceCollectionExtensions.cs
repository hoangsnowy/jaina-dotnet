using Jaina.Notifications.Email;
using Jaina.Notifications.Sms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Notifications;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaSmtpEmail(this IServiceCollection services, Action<SmtpEmailSenderOptions> configure)
    {
        services.AddOptions<SmtpEmailSenderOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }

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
