using System.Collections.Concurrent;

namespace Jaina.Messaging.Outbox.InMemory;

/// <summary>
/// Thread-safe in-process <see cref="IOutboxStore"/>. Loses state on restart and is not safe
/// across multiple processes — use the EF Core or Redis provider in production.
/// </summary>
public sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();
    private readonly ConcurrentDictionary<Guid, byte> _claimed = new();

    public Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var claimed = new List<OutboxMessage>(batchSize);

        foreach (var msg in _messages.Values)
        {
            if (claimed.Count >= batchSize) break;
            if (msg.ProcessedAt is not null) continue;
            if (msg.ScheduledFor > now) continue;
            if (!_claimed.TryAdd(msg.Id, 0)) continue;
            claimed.Add(msg);
        }

        return Task.FromResult<IReadOnlyList<OutboxMessage>>(claimed);
    }

    public Task MarkProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
            msg.ProcessedAt = DateTimeOffset.UtcNow;
        _claimed.TryRemove(messageId, out _);
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid messageId, string error, DateTimeOffset nextAttemptAt, CancellationToken ct = default)
    {
        if (_messages.TryGetValue(messageId, out var existing))
        {
            // OutboxMessage is mostly init-only; replace via a new instance preserving immutable fields
            var updated = new OutboxMessage
            {
                Id = existing.Id,
                PayloadType = existing.PayloadType,
                Payload = existing.Payload,
                Destination = existing.Destination,
                Headers = existing.Headers,
                CreatedAt = existing.CreatedAt,
                ScheduledFor = nextAttemptAt,
                ProcessedAt = null,
                Attempts = existing.Attempts + 1,
                LastError = error,
            };
            _messages[messageId] = updated;
        }
        _claimed.TryRemove(messageId, out _);
        return Task.CompletedTask;
    }

    /// <summary>Test helper — returns a snapshot of all messages (processed + pending).</summary>
    public IReadOnlyCollection<OutboxMessage> Snapshot() => _messages.Values.ToArray();
}
