using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Jaina.Caching.Redis;

public class RedisCache : ICache
{
    private readonly RedisConnectionManager _manager;
    private readonly string _instanceName;
    private readonly ILogger<RedisCache> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private IDatabase? _database;

    public RedisCache(RedisConnectionManager manager, IOptions<RedisCacheOptions> options, ILogger<RedisCache> logger)
    {
        _manager = manager;
        _instanceName = options.Value.InstanceName;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public bool IsDistributed => true;

    private IDatabase Database
    {
        get
        {
            if (_database is not null) return _database;
            try
            {
                _database = _manager.Connection.GetDatabase();
            }
            catch (Exception ex) when (ex is RedisConnectionException or SocketException)
            {
                _manager.ForceReconnect();
                _database = _manager.Connection.GetDatabase();
            }
            return _database;
        }
    }

    private string PrefixKey(string key) =>
        string.IsNullOrEmpty(_instanceName) ? key : $"{_instanceName}:{key}";

    private RedisKey SlidingKey(string key) => $"SE_{PrefixKey(key)}";

    public void Set<T>(string key, T value, TimeSpan expiry)
    {
        if (value is null) return;
        Database.StringSet(PrefixKey(key), JsonSerializer.Serialize(value, _jsonOptions), expiry);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default)
    {
        if (value is null) return;
        await Database.StringSetAsync(PrefixKey(key), JsonSerializer.Serialize(value, _jsonOptions), expiry).ConfigureAwait(false);
    }

    public void Set<T>(string key, T value, CacheEntryOptions options)
    {
        if (value is null) return;
        var prefixed = PrefixKey(key);
        var json = JsonSerializer.Serialize(value, _jsonOptions);

        if (options.AbsoluteExpiration.HasValue)
        {
            var expiry = options.AbsoluteExpiration.Value.UtcDateTime - DateTime.UtcNow;
            Database.StringSet(prefixed, json, expiry);
        }
        else if (options.SlidingExpiration.HasValue)
        {
            Database.StringSet(prefixed, json, options.SlidingExpiration.Value);
            Database.StringSet(SlidingKey(key), options.SlidingExpiration.Value.TotalSeconds, options.SlidingExpiration.Value);
        }
        else if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            Database.StringSet(prefixed, json, options.AbsoluteExpirationRelativeToNow.Value);
        }
    }

    public async Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct = default)
    {
        if (value is null) return;
        var prefixed = PrefixKey(key);
        var json = JsonSerializer.Serialize(value, _jsonOptions);

        if (options.AbsoluteExpiration.HasValue)
        {
            var expiry = options.AbsoluteExpiration.Value.UtcDateTime - DateTime.UtcNow;
            await Database.StringSetAsync(prefixed, json, expiry).ConfigureAwait(false);
        }
        else if (options.SlidingExpiration.HasValue)
        {
            await Task.WhenAll(
                Database.StringSetAsync(prefixed, json, options.SlidingExpiration.Value),
                Database.StringSetAsync(SlidingKey(key), options.SlidingExpiration.Value.TotalSeconds, options.SlidingExpiration.Value)
            ).ConfigureAwait(false);
        }
        else if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            await Database.StringSetAsync(prefixed, json, options.AbsoluteExpirationRelativeToNow.Value).ConfigureAwait(false);
        }
    }

    public T? Get<T>(string key)
    {
        var value = Database.StringGet(PrefixKey(key));
        if (value.IsNull) return default;

        RefreshSliding(key);
        return JsonSerializer.Deserialize<T>((string)value!, _jsonOptions);
    }

    public T Get<T>(string key, TimeSpan expiry, Func<T> factory)
    {
        var value = Get<T>(key);
        if (EqualityComparer<T>.Default.Equals(value!, default!))
        {
            var newValue = factory();
            Set(key, newValue, expiry);
            return newValue;
        }
        return value!;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await Database.StringGetAsync(PrefixKey(key)).ConfigureAwait(false);
        if (value.IsNull) return default;

        await RefreshSlidingAsync(key).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>((string)value!, _jsonOptions);
    }

    public async Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<T> factory, CancellationToken ct = default)
    {
        var value = await GetAsync<T>(key, ct).ConfigureAwait(false);
        if (EqualityComparer<T>.Default.Equals(value!, default!))
        {
            var newValue = factory();
            await SetAsync(key, newValue, expiry, ct).ConfigureAwait(false);
            return newValue;
        }
        return value!;
    }

    public async Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<Task<T>> factory, CancellationToken ct = default)
    {
        var value = await GetAsync<T>(key, ct).ConfigureAwait(false);
        if (EqualityComparer<T>.Default.Equals(value!, default!))
        {
            var newValue = await factory().ConfigureAwait(false);
            await SetAsync(key, newValue, expiry, ct).ConfigureAwait(false);
            return newValue;
        }
        return value!;
    }

    public void Remove(string key)
    {
        Database.KeyDelete(PrefixKey(key));
        Database.KeyDelete(SlidingKey(key));
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await Task.WhenAll(
            Database.KeyDeleteAsync(PrefixKey(key)),
            Database.KeyDeleteAsync(SlidingKey(key))
        ).ConfigureAwait(false);
    }

    private void RefreshSliding(string key)
    {
        var slidingKey = SlidingKey(key);
        if (!Database.KeyExists(slidingKey)) return;
        var seconds = double.Parse(Database.StringGet(slidingKey)!);
        var expiry = DateTime.UtcNow.AddSeconds(seconds);
        Database.KeyExpire(PrefixKey(key), expiry);
        Database.KeyExpire(slidingKey, expiry);
    }

    private async Task RefreshSlidingAsync(string key)
    {
        var slidingKey = SlidingKey(key);
        if (!await Database.KeyExistsAsync(slidingKey).ConfigureAwait(false)) return;
        var val = await Database.StringGetAsync(slidingKey).ConfigureAwait(false);
        var seconds = double.Parse(val!);
        var expiry = DateTime.UtcNow.AddSeconds(seconds);
        await Task.WhenAll(
            Database.KeyExpireAsync(PrefixKey(key), expiry),
            Database.KeyExpireAsync(slidingKey, expiry)
        ).ConfigureAwait(false);
    }
}
