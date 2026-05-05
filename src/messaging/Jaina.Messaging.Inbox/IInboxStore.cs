namespace Jaina.Messaging.Inbox;

/// <summary>
/// Records (consumer, message-id) pairs so a duplicate message can be detected and skipped.
/// Implementations must atomically check-and-set: <see cref="TryConsumeAsync"/> returns
/// <c>true</c> only the first time a given (consumer, id) is seen.
/// </summary>
public interface IInboxStore
{
    /// <summary>
    /// Atomically claim the message for the given consumer. Returns <c>true</c> if this is
    /// the first time the consumer is seeing this id (so the caller should process), or
    /// <c>false</c> if already seen (caller should ack-and-skip).
    /// </summary>
    /// <param name="consumer">Logical consumer name (service id, handler name, etc.).</param>
    /// <param name="messageId">Stable message identifier supplied by the producer.</param>
    /// <param name="ttl">How long to retain the dedup record before allowing reprocessing.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryConsumeAsync(string consumer, string messageId, TimeSpan ttl, CancellationToken ct = default);
}
