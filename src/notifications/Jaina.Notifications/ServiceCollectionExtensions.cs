using Jaina.Notifications.Email;
using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Notifications;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJainaSmtpEmail(this IServiceCollection services, Action<SmtpEmailSenderOptions> configure)
    {
        services.AddOptions<SmtpEmailSenderOptions>()
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
