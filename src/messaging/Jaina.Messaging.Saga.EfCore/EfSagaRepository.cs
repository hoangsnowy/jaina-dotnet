using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Jaina.Messaging.Saga.EfCore;

/// <summary>
/// EF Core <see cref="ISagaRepository{TState}"/>. Serializes the saga state subclass to
/// JSON and stores one row per correlation id. Survives restarts and is shared across
/// processes via the underlying database.
/// </summary>
public sealed class EfSagaRepository<TState, TDbContext> : ISagaRepository<TState>
    where TState : SagaState
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public EfSagaRepository(IDbContextFactory<TDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<TState?> LoadAsync(Guid correlationId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var record = await ctx.Set<SagaStateRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == correlationId, ct);

        if (record is null) return null;
        return JsonSerializer.Deserialize<TState>(record.Payload);
    }

    public async Task SaveAsync(TState state, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct);
        var existing = await ctx.Set<SagaStateRecord>()
            .FirstOrDefaultAsync(s => s.CorrelationId == state.CorrelationId, ct);

        var payload = JsonSerializer.Serialize(state);
        if (existing is null)
        {
            ctx.Set<SagaStateRecord>().Add(new SagaStateRecord
            {
                CorrelationId = state.CorrelationId,
                StateType = typeof(TState).FullName ?? typeof(TState).Name,
                Payload = payload,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            // Existing record can't be replaced via init-only — remove + add
            ctx.Set<SagaStateRecord>().Remove(existing);
            await ctx.SaveChangesAsync(ct);

            await using var ctx2 = await _factory.CreateDbContextAsync(ct);
            ctx2.Set<SagaStateRecord>().Add(new SagaStateRecord
            {
                CorrelationId = state.CorrelationId,
                StateType = typeof(TState).FullName ?? typeof(TState).Name,
                Payload = payload,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await ctx2.SaveChangesAsync(ct);
            return;
        }
        await ctx.SaveChangesAsync(ct);
    }
}
