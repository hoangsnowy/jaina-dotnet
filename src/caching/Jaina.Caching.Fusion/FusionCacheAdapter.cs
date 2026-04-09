using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Jaina.Caching.Fusion;

public class FusionCacheOptions
{
    public string? RedisConnectionString { get; set; }
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(5);
}

public class FusionCacheAdapter : ICache, IDistributedLock
{
    private readonly IFusionCache _cache;

    public FusionCacheAdapter(IFusionCache cache)
    {
        _cache = cache;
    }

    public bool IsDistributed => true;

    public void Set<T>(string key, T value, TimeSpan expiry) =>
        _cache.Set(key, value, expiry);

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) =>
        await _cache.SetAsync(key, value, expiry, token: ct).ConfigureAwait(false);

    public void Set<T>(string key, T value, CacheEntryOptions options) =>
        _cache.Set(key, value, CreateEntryOptions(options));

    public async Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct = default) =>
        await _cache.SetAsync(key, value, CreateEntryOptions(options), token: ct).ConfigureAwait(false);

    public T? Get<T>(string key) => _cache.TryGet<T>(key).GetValueOrDefault();

    public T Get<T>(string key, TimeSpan expiry, Func<T> factory) =>
        _cache.GetOrSet(key, _ => factory(), expiry)!;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var result = await _cache.TryGetAsync<T>(key, token: ct).ConfigureAwait(false);
        return result.GetValueOrDefault();
    }

    public async Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<T> factory, CancellationToken ct = default) =>
        (await _cache.GetOrSetAsync(key, _ => Task.FromResult(factory()), expiry, token: ct).ConfigureAwait(false))!;

    public async Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<Task<T>> factory, CancellationToken ct = default) =>
        (await _cache.GetOrSetAsync(key, _ => factory(), expiry, token: ct).ConfigureAwait(false))!;

    public void Remove(string key) => _cache.Remove(key);

    public async Task RemoveAsync(string key, CancellationToken ct = default) =>
        await _cache.RemoveAsync(key, token: ct).ConfigureAwait(false);

    public async Task<bool> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var result = await _cache.TryGetAsync<bool>($"lock:{key}", token: ct).ConfigureAwait(false);
        if (result.HasValue) return false;
        await _cache.SetAsync($"lock:{key}", true, expiry, token: ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> ReleaseAsync(string key, CancellationToken ct = default)
    {
        await _cache.RemoveAsync($"lock:{key}", token: ct).ConfigureAwait(false);
        return true;
    }

    private static FusionCacheEntryOptions CreateEntryOptions(CacheEntryOptions options)
    {
        var entry = new FusionCacheEntryOptions();
        if (options.SlidingExpiration.HasValue)
            entry.Duration = options.SlidingExpiration.Value;
        else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            entry.Duration = options.AbsoluteExpirationRelativeToNow.Value;
        else if (options.AbsoluteExpiration.HasValue)
            entry.Duration = options.AbsoluteExpiration.Value - DateTimeOffset.UtcNow;
        return entry;
    }
}
