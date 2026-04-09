using LazyCache;

namespace Jaina.Caching.Memory;

public class MemoryCache : ICache
{
    private readonly IAppCache _cache;

    public MemoryCache()
    {
        _cache = new CachingService();
    }

    public bool IsDistributed => false;

    public void Set<T>(string key, T value, TimeSpan expiry) =>
        _cache.Add(key, value, DateTimeOffset.UtcNow.Add(expiry));

    public Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default)
    {
        Set(key, value, expiry);
        return Task.CompletedTask;
    }

    public void Set<T>(string key, T value, CacheEntryOptions options)
    {
        if (options.AbsoluteExpiration.HasValue)
            _cache.Add(key, value, options.AbsoluteExpiration.Value);
        else if (options.SlidingExpiration.HasValue)
            _cache.Add(key, value, options.SlidingExpiration.Value);
        else if (options.AbsoluteExpirationRelativeToNow.HasValue)
            _cache.Add(key, value, DateTimeOffset.UtcNow.Add(options.AbsoluteExpirationRelativeToNow.Value));
    }

    public Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public T? Get<T>(string key) => _cache.Get<T>(key);

    public T Get<T>(string key, TimeSpan expiry, Func<T> factory) =>
        _cache.GetOrAdd(key, factory, expiry);

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
        _cache.GetAsync<T?>(key);

    public Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<T> factory, CancellationToken ct = default) =>
        Task.FromResult(_cache.GetOrAdd(key, factory, expiry));

    public Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<Task<T>> factory, CancellationToken ct = default) =>
        _cache.GetOrAddAsync(key, factory, expiry);

    public void Remove(string key) => _cache.Remove(key);

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
