using Jaina.Notifications.Email;
using Jaina.Notifications.Smtp.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jaina.Notifications.Smtp;

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
}
