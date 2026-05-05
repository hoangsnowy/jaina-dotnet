using Jaina.Messaging.Inbox;
using Jaina.Messaging.Inbox.InMemory;
using Microsoft.Extensions.Caching.Memory;

namespace Jaina.Messaging.Inbox.UnitTests;

public class InMemoryInboxStoreTests
{
    private readonly InMemoryInboxStore _store = new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task TryConsume_FirstSeen_ReturnsTrue()
    {
        // Act
        var ok = await _store.TryConsumeAsync("orders-svc", "msg-1", TimeSpan.FromMinutes(5));

        // Assert
        Assert.True(ok);
    }

    [Fact]
    public async Task TryConsume_DuplicateForSameConsumer_ReturnsFalse()
    {
        // Arrange — first call claims it
        await _store.TryConsumeAsync("orders-svc", "msg-1", TimeSpan.FromMinutes(5));

        // Act — second call sees a duplicate
        var ok = await _store.TryConsumeAsync("orders-svc", "msg-1", TimeSpan.FromMinutes(5));

        // Assert
        Assert.False(ok);
    }

    [Fact]
    public async Task TryConsume_DifferentConsumers_BothSucceed()
    {
        // Same message, different consumer → fan-out scenario, each consumer should process once
        var a = await _store.TryConsumeAsync("orders-svc", "msg-1", TimeSpan.FromMinutes(5));
        var b = await _store.TryConsumeAsync("billing-svc", "msg-1", TimeSpan.FromMinutes(5));

        Assert.True(a);
        Assert.True(b);
    }

    [Fact]
    public async Task TryConsume_AfterTtl_AllowsReprocessing()
    {
        // Arrange — claim with a tiny TTL
        await _store.TryConsumeAsync("svc", "expired", TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        // Act — TTL passed, store should accept again
        var ok = await _store.TryConsumeAsync("svc", "expired", TimeSpan.FromMinutes(5));

        // Assert
        Assert.True(ok);
    }
}
