using Microsoft.Extensions.Caching.Memory;

namespace Jaina.Messaging.Inbox.InMemory;

/// <summary>
/// IMemoryCache-backed inbox store. Single-process scope — use Redis or EF Core in
/// production where multiple consumer instances need to share dedup state.
/// </summary>
public sealed class InMemoryInboxStore : IInboxStore
{
    private readonly IMemoryCache _cache;
    private readonly object _lock = new();

    public InMemoryInboxStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<bool> TryConsumeAsync(string consumer, string messageId, TimeSpan ttl, CancellationToken ct = default)
    {
        var key = Compose(consumer, messageId);

        // Lock-and-check: IMemoryCache has no atomic SETNX so guard the (read, write) sequence
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out _))
                return Task.FromResult(false);

            _cache.Set(key, true, ttl);
            return Task.FromResult(true);
        }
    }

    private static string Compose(string consumer, string messageId) =>
        $"jaina.inbox.{consumer}::{messageId}";
}
