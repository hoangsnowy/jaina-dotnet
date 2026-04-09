using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Jaina.Caching.Redis;

public class RedisDistributedLock : IDistributedLock
{
    private readonly RedisConnectionManager _manager;
    private readonly string _instanceName;

    public RedisDistributedLock(RedisConnectionManager manager, IOptions<RedisCacheOptions> options)
    {
        _manager = manager;
        _instanceName = options.Value.InstanceName;
    }

    private string PrefixKey(string key) =>
        string.IsNullOrEmpty(_instanceName) ? $"lock:{key}" : $"{_instanceName}:lock:{key}";

    public async Task<bool> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var db = _manager.Connection.GetDatabase();
        return await db.StringSetAsync(PrefixKey(key), Environment.MachineName, expiry, When.NotExists).ConfigureAwait(false);
    }

    public async Task<bool> ReleaseAsync(string key, CancellationToken ct = default)
    {
        var db = _manager.Connection.GetDatabase();
        return await db.KeyDeleteAsync(PrefixKey(key)).ConfigureAwait(false);
    }
}
