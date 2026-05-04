using Microsoft.Extensions.Caching.Memory;

namespace Jaina.Caching.Memory;

public class MemoryCache : ICache
{
    private readonly IMemoryCache _cache;

    public MemoryCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool IsDistributed => false;

    public void Set<T>(string key, T value, TimeSpan expiry) =>
        _cache.Set(key, value, expiry);

    public Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default)
    {
        Set(key, value, expiry);
        return Task.CompletedTask;
    }

    public void Set<T>(string key, T value, CacheEntryOptions options) =>
        _cache.Set(key, value, ToMemoryOptions(options));

    public Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public T? Get<T>(string key) => _cache.Get<T>(key);

    public T Get<T>(string key, TimeSpan expiry, Func<T> factory) =>
        _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = expiry;
            return factory();
        })!;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
        Task.FromResult(_cache.Get<T>(key));

    public Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<T> factory, CancellationToken ct = default) =>
        Task.FromResult(Get(key, expiry, factory));

    public Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<Task<T>> factory, CancellationToken ct = default) =>
        _cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = expiry;
            return factory();
        })!;

    public void Remove(string key) => _cache.Remove(key);

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    private static MemoryCacheEntryOptions ToMemoryOptions(CacheEntryOptions options)
    {
        var opts = new MemoryCacheEntryOptions();
        if (options.AbsoluteExpiration.HasValue)
            opts.AbsoluteExpiration = options.AbsoluteExpiration.Value;
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
            opts.AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow.Value;
        if (options.SlidingExpiration.HasValue)
            opts.SlidingExpiration = options.SlidingExpiration.Value;
        return opts;
    }
}
