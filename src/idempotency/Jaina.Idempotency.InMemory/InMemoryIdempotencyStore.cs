using Microsoft.Extensions.Caching.Memory;

namespace Jaina.Idempotency.InMemory;

/// <summary>
/// In-process <see cref="IIdempotencyStore"/> backed by <see cref="IMemoryCache"/>. Loses
/// state on restart and is not safe across multiple processes — use Redis or another
/// distributed provider in production.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly IMemoryCache _cache;

    public InMemoryIdempotencyStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<IdempotencyEntry?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(_cache.Get<IdempotencyEntry?>(Compose(key)));

    public Task SetAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken ct = default)
    {
        _cache.Set(Compose(key), entry, ttl);
        return Task.CompletedTask;
    }

    private static string Compose(string key) => $"jaina.idem.{key}";
}
