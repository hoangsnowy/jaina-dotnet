using System.Text.Json;
using StackExchange.Redis;

namespace Jaina.Messaging.Saga.Redis;

/// <summary>
/// Redis-backed <see cref="ISagaRepository{TState}"/>. Serializes the saga state subclass
/// to JSON and stores one key per correlation id. Survives restart and is shared across
/// processes via the Redis cluster.
/// </summary>
public sealed class RedisSagaRepository<TState> : ISagaRepository<TState>
    where TState : SagaState
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;

    public RedisSagaRepository(IConnectionMultiplexer redis, string keyPrefix = "jaina:saga:")
    {
        _redis = redis;
        _keyPrefix = keyPrefix;
    }

    public async Task<TState?> LoadAsync(Guid correlationId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var raw = await db.StringGetAsync(_keyPrefix + correlationId);
        if (raw.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<TState>((string)raw!);
    }

    public async Task SaveAsync(TState state, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(state);
        // Saga state retained indefinitely — let operator clean up post-completion.
        // Could add TTL after IsCompleted/IsCompensated as a future improvement.
        await db.StringSetAsync(_keyPrefix + state.CorrelationId, json);
    }
}
