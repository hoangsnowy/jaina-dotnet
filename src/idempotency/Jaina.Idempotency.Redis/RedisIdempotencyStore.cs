using System.Text.Json;
using StackExchange.Redis;

namespace Jaina.Idempotency.Redis;

/// <summary>
/// Redis-backed <see cref="IIdempotencyStore"/>. Survives restart, shared across instances.
/// Uses a single string key per idempotency token; entry serialized as JSON.
/// </summary>
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;

    public RedisIdempotencyStore(IConnectionMultiplexer redis, string keyPrefix = "jaina:idem:")
    {
        _redis = redis;
        _keyPrefix = keyPrefix;
    }

    public async Task<IdempotencyEntry?> GetAsync(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var raw = await db.StringGetAsync(_keyPrefix + key);
        if (raw.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<IdempotencyEntry>((string)raw!);
    }

    public async Task SetAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(entry);
        await db.StringSetAsync(_keyPrefix + key, json, ttl);
    }
}
