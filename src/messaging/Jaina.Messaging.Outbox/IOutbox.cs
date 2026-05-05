namespace Jaina.Messaging.Outbox;

/// <summary>
/// Producer-side API. Domain code calls <see cref="EnqueueAsync{T}"/> within a unit of work;
/// the message is committed atomically with the rest of the transaction and dispatched
/// asynchronously by the relay. Implementations decide where to persist (EF Core, Dapper, etc.)
/// and how to enlist in the ambient transaction.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Enqueue a message for asynchronous dispatch. The message is not delivered until the
    /// caller's unit of work commits — implementations must enlist with the surrounding
    /// transaction or write into the same DbContext.
    /// </summary>
    /// <param name="message">Domain message to dispatch.</param>
    /// <param name="destination">Optional logical destination (queue/topic name).</param>
    /// <param name="headers">Optional headers (correlation id, tenant, schema version, ...).</param>
    /// <param name="scheduledFor">Optional UTC time before which dispatch must not occur.</param>
    /// <param name="ct">Cancellation token for the enqueue operation.</param>
    Task EnqueueAsync<T>(
        T message,
        string? destination = null,
        IDictionary<string, string>? headers = null,
        DateTimeOffset? scheduledFor = null,
        CancellationToken ct = default);
}
