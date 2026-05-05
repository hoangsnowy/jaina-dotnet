namespace Jaina.Messaging.Outbox;

/// <summary>
/// Relay-side primitives for claiming, marking, and parking outbox messages. Providers
/// implement this against their persistence engine (EF Core, Dapper, Redis, etc.).
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Claim up to <paramref name="batchSize"/> pending messages whose
    /// <see cref="OutboxMessage.ScheduledFor"/> is at or before now. Implementations must
    /// guarantee that two relay instances cannot claim the same message concurrently.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(int batchSize, CancellationToken ct = default);

    /// <summary>Mark a message as successfully dispatched.</summary>
    Task MarkProcessedAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>Record a dispatch failure and reschedule for retry per <paramref name="nextAttemptAt"/>.</summary>
    Task MarkFailedAsync(Guid messageId, string error, DateTimeOffset nextAttemptAt, CancellationToken ct = default);

    /// <summary>Persist a new message (used by <see cref="IOutbox"/> implementations).</summary>
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
}
