using Microsoft.EntityFrameworkCore;

namespace Jaina.Messaging.Inbox.EfCore;

/// <summary>
/// EF Core <see cref="IInboxStore"/>. Atomicity comes from the unique composite key on
/// (Consumer, MessageId): a duplicate insert raises <see cref="DbUpdateException"/> which
/// the store catches and returns <c>false</c>. First writer wins; duplicates skip.
/// </summary>
public sealed class EfInboxStore<TDbContext> : IInboxStore
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public EfInboxStore(IDbContextFactory<TDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<bool> TryConsumeAsync(string consumer, string messageId, TimeSpan ttl, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);

        // Fast-path read first; if we see a non-expired record, dedup hit
        var existing = await ctx.Set<InboxRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Consumer == consumer && r.MessageId == messageId, ct);

        if (existing is not null && existing.ExpiresAt > DateTimeOffset.UtcNow)
            return false;

        if (existing is not null)
        {
            // Stale — drop and re-insert below
            ctx.Set<InboxRecord>().Remove(new InboxRecord { Consumer = consumer, MessageId = messageId });
            await ctx.SaveChangesAsync(ct);
            await using var ctx2 = await _factory.CreateDbContextAsync(ct);
            return await InsertAsync(ctx2, consumer, messageId, ttl, ct);
        }

        return await InsertAsync(ctx, consumer, messageId, ttl, ct);
    }

    private static async Task<bool> InsertAsync(DbContext ctx, string consumer, string messageId, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            ctx.Set<InboxRecord>().Add(new InboxRecord
            {
                Consumer = consumer,
                MessageId = messageId,
                ExpiresAt = DateTimeOffset.UtcNow + ttl,
            });
            await ctx.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Concurrent insert raced us — duplicate.
            return false;
        }
    }
}
