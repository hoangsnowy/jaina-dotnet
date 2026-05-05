using Jaina.Messaging.Outbox;

namespace JainaShop.Orders;

/// <summary>
/// Sample dispatcher — production code would publish to RabbitMQ / ServiceBus / Kafka here.
/// </summary>
public sealed class ConsoleOutboxDispatcher : IOutboxDispatcher
{
    private readonly ILogger<ConsoleOutboxDispatcher> _logger;
    public ConsoleOutboxDispatcher(ILogger<ConsoleOutboxDispatcher> logger) => _logger = logger;

    public Task DispatchAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[outbox] dispatch {Id} type={Type} dest={Destination} payload={Payload}",
            message.Id, message.PayloadType, message.Destination, message.Payload);
        return Task.CompletedTask;
    }
}
