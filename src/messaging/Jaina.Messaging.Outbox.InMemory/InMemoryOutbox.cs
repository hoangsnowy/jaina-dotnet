using System.Text.Json;

namespace Jaina.Messaging.Outbox.InMemory;

/// <summary>
/// Producer that writes messages directly to the underlying <see cref="InMemoryOutboxStore"/>.
/// In an EF/Dapper world the producer would defer the write until <c>SaveChanges</c>; the
/// in-memory provider commits immediately.
/// </summary>
public sealed class InMemoryOutbox : IOutbox
{
    private readonly IOutboxStore _store;

    public InMemoryOutbox(IOutboxStore store)
    {
        _store = store;
    }

    public Task EnqueueAsync<T>(
        T message,
        string? destination = null,
        IDictionary<string, string>? headers = null,
        DateTimeOffset? scheduledFor = null,
        CancellationToken ct = default)
    {
        var entry = new OutboxMessage
        {
            PayloadType = typeof(T).FullName ?? typeof(T).Name,
            Payload = JsonSerializer.Serialize(message),
            Destination = destination,
            Headers = headers ?? new Dictionary<string, string>(),
            ScheduledFor = scheduledFor ?? DateTimeOffset.UtcNow,
        };
        return _store.AddAsync(entry, ct);
    }
}
