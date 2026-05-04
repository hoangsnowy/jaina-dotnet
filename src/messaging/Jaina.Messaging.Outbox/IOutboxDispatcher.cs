namespace Jaina.Messaging.Outbox;

/// <summary>
/// Translates an <see cref="OutboxMessage"/> into a real broker call (RabbitMQ publish,
/// ServiceBus send, Kafka produce, etc.). Provided by the application — the relay invokes
/// this for each claimed message.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>
    /// Dispatch the given message. Throw to signal a transient failure; the relay will mark
    /// the message as failed, increment its attempt counter, and reschedule per the configured
    /// backoff. Return successfully to mark the message as processed.
    /// </summary>
    Task DispatchAsync(OutboxMessage message, CancellationToken ct = default);
}
