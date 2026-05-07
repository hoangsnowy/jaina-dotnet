using Jaina.Notifications.Sms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace JainaShop.Notifier;

internal sealed class ConsoleSmsLogger : ISmsSender
{
    private readonly ILogger<ConsoleSmsLogger> _logger;

    public ConsoleSmsLogger(ILogger<ConsoleSmsLogger> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[SMS] From: {From} | To: {To} | Body: {Body}",
            message.From,
            message.To,
            message.Body);

        return Task.CompletedTask;
    }
}

internal static class ConsoleSmsServiceCollectionExtensions
{
    public static IServiceCollection AddSampleConsoleSms(this IServiceCollection services)
    {
        services.TryAddSingleton<ISmsSender, ConsoleSmsLogger>();
        return services;
    }
}
