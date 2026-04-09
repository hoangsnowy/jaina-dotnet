namespace Jaina.Caching;

public interface ICache
{
    bool IsDistributed { get; }

    void Set<T>(string key, T value, TimeSpan expiry);
    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default);

    void Set<T>(string key, T value, CacheEntryOptions options);
    Task SetAsync<T>(string key, T value, CacheEntryOptions options, CancellationToken ct = default);

    T? Get<T>(string key);
    T Get<T>(string key, TimeSpan expiry, Func<T> factory);

    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<T> factory, CancellationToken ct = default);
    Task<T> GetAsync<T>(string key, TimeSpan expiry, Func<Task<T>> factory, CancellationToken ct = default);

    void Remove(string key);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
