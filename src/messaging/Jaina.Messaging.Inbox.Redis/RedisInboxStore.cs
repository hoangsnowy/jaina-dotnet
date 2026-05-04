using StackExchange.Redis;

namespace Jaina.Messaging.Inbox.Redis;

/// <summary>
/// Redis-backed <see cref="IInboxStore"/>. Uses <c>SET key value NX EX ttl</c> for atomic
/// claim — first writer wins, subsequent attempts return false. Suitable for HA consumer
/// fleets where multiple instances can receive the same message.
/// </summary>
public sealed class RedisInboxStore : IInboxStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _keyPrefix;

    public RedisInboxStore(IConnectionMultiplexer redis, string keyPrefix = "jaina:inbox:")
    {
        _redis = redis;
        _keyPrefix = keyPrefix;
    }

    public async Task<bool> TryConsumeAsync(string consumer, string messageId, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{_keyPrefix}{consumer}::{messageId}";
        // SET key value NX EX ttl — atomic, returns true only if key did not exist
        return await db.StringSetAsync(key, "1", ttl, When.NotExists);
    }
}
