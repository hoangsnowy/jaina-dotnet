using Jaina.Idempotency;
using Jaina.Idempotency.InMemory;
using Microsoft.Extensions.Caching.Memory;

namespace Jaina.Idempotency.UnitTests;

public class InMemoryIdempotencyStoreTests
{
    private readonly InMemoryIdempotencyStore _store = new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        // Act
        var result = await _store.GetAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetThenGet_ReturnsStoredEntry()
    {
        // Arrange
        var entry = new IdempotencyEntry(201, "application/json", new byte[] { 1, 2, 3 }, DateTimeOffset.UtcNow);

        // Act
        await _store.SetAsync("key1", entry, TimeSpan.FromMinutes(5));
        var result = await _store.GetAsync("key1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(201, result!.StatusCode);
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Body);
    }

    [Fact]
    public async Task Set_Overwrites_ExistingEntry()
    {
        // Arrange
        var first = new IdempotencyEntry(200, null, [], DateTimeOffset.UtcNow);
        var second = new IdempotencyEntry(202, "text/plain", [9], DateTimeOffset.UtcNow);

        // Act
        await _store.SetAsync("key2", first, TimeSpan.FromMinutes(5));
        await _store.SetAsync("key2", second, TimeSpan.FromMinutes(5));
        var result = await _store.GetAsync("key2");

        // Assert
        Assert.Equal(202, result!.StatusCode);
        Assert.Equal("text/plain", result.ContentType);
    }
}
