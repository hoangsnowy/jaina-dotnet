namespace Jaina.Messaging.Outbox;

/// <summary>
/// A message persisted to the outbox by a domain operation. The relay claims pending
/// messages, dispatches them via <see cref="IOutboxDispatcher"/>, then marks them as
/// processed (or failed, with retry semantics).
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Stable identifier; serves as the deduplication key for downstream consumers.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>CLR type name of the payload (assembly-qualified or short, depending on options).</summary>
    public string PayloadType { get; init; } = string.Empty;

    /// <summary>Serialized payload (typically JSON).</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>Optional logical destination (queue/topic/exchange name).</summary>
    public string? Destination { get; init; }

    /// <summary>Free-form headers (correlation id, tenant id, schema version, etc.).</summary>
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>UTC timestamp when the message was enqueued.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp at which the relay should first attempt dispatch (for delayed messages).</summary>
    public DateTimeOffset ScheduledFor { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when the message was successfully dispatched. Null while pending.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Number of dispatch attempts so far.</summary>
    public int Attempts { get; set; }

    /// <summary>Last error message if dispatch failed; cleared on success.</summary>
    public string? LastError { get; set; }
}
