using Microsoft.Extensions.Logging;

namespace Jaina.Notifications.Sms;

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
