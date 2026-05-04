using Microsoft.EntityFrameworkCore;

namespace Jaina.Messaging.Outbox.EfCore;

/// <summary>
/// Relay-side <see cref="IOutboxStore"/> backed by <typeparamref name="TDbContext"/>. Uses
/// <see cref="IDbContextFactory{TContext}"/> so each operation owns its scope and connection
/// — important for the relay's polling loop, which runs outside any HTTP request scope.
/// </summary>
/// <remarks>
/// MVP single-relay assumption: <see cref="ClaimBatchAsync"/> does not currently use
/// <c>FOR UPDATE SKIP LOCKED</c> or row-versioning, so running multiple relay instances
/// against the same store can double-dispatch. Run exactly one relay per outbox table until
/// the multi-relay claim mechanism lands (tracked in M1 wrap-up).
/// </remarks>
public sealed class EfOutboxStore<TDbContext> : IOutboxStore
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public EfOutboxStore(IDbContextFactory<TDbContext> factory)
    {
        _factory = factory;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        ctx.Set<OutboxMessage>().Add(message);
        await ctx.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(int batchSize, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var batch = await ctx.Set<OutboxMessage>()
            .AsTracking()
            .Where(m => m.ProcessedAt == null && m.ScheduledFor <= now)
            .OrderBy(m => m.ScheduledFor)
            .Take(batchSize)
            .ToListAsync(ct);

        return batch;
    }

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var msg = await ctx.Set<OutboxMessage>().FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null) return;

        msg.ProcessedAt = DateTimeOffset.UtcNow;
        msg.LastError = null;
        await ctx.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid messageId, string error, DateTimeOffset nextAttemptAt, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var msg = await ctx.Set<OutboxMessage>().FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is null) return;

        msg.Attempts += 1;
        msg.LastError = error;
        // Re-create with new ScheduledFor (init-only) — load+detach+add a new copy preserving the rest
        var rescheduled = new OutboxMessage
        {
            Id = msg.Id,
            PayloadType = msg.PayloadType,
            Payload = msg.Payload,
            Destination = msg.Destination,
            Headers = msg.Headers,
            CreatedAt = msg.CreatedAt,
            ScheduledFor = nextAttemptAt,
            ProcessedAt = null,
            Attempts = msg.Attempts,
            LastError = error,
        };
        ctx.Entry(msg).State = EntityState.Detached;
        ctx.Set<OutboxMessage>().Update(rescheduled);
        await ctx.SaveChangesAsync(ct);
    }
}
