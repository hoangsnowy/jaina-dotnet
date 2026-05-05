using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Jaina.Messaging.Outbox.EfCore;

/// <summary>
/// Producer that adds an <see cref="OutboxMessage"/> to the user's <typeparamref name="TDbContext"/>.
/// The user's <c>SaveChangesAsync</c> commits the message atomically with their domain writes —
/// no dual-write problem.
/// </summary>
public sealed class EfOutbox<TDbContext> : IOutbox
    where TDbContext : DbContext
{
    private readonly TDbContext _db;

    public EfOutbox(TDbContext db)
    {
        _db = db;
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

        _db.Set<OutboxMessage>().Add(entry);
        // Intentionally NOT calling SaveChangesAsync — caller owns the transaction.
        return Task.CompletedTask;
    }
}
