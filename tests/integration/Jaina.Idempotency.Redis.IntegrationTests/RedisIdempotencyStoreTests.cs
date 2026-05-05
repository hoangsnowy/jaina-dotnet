using Jaina.Idempotency;
using Jaina.Idempotency.Redis;

namespace Jaina.Idempotency.Redis.IntegrationTests;

[Collection("redis")]
public class RedisIdempotencyStoreTests : IClassFixture<RedisIdempotencyStoreFixture>
{
    private readonly RedisIdempotencyStore _store;

    public RedisIdempotencyStoreTests(RedisIdempotencyStoreFixture fixture)
    {
        _store = new RedisIdempotencyStore(fixture.Connection, $"test:{Guid.NewGuid():N}:");
    }

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        // Act
        var result = await _store.GetAsync("nope");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetThenGet_RoundTripsAcrossRedis()
    {
        // Arrange
        var entry = new IdempotencyEntry(201, "application/json", new byte[] { 1, 2, 3 }, DateTimeOffset.UtcNow);

        // Act
        await _store.SetAsync("k1", entry, TimeSpan.FromMinutes(5));
        var result = await _store.GetAsync("k1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(201, result!.StatusCode);
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Body);
    }

    [Fact]
    public async Task Set_HonoursTtl()
    {
        // Arrange — write with 1s TTL
        await _store.SetAsync("expiring", new IdempotencyEntry(200, null, [], DateTimeOffset.UtcNow), TimeSpan.FromSeconds(1));

        // Act — wait past expiry
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        var result = await _store.GetAsync("expiring");

        // Assert
        Assert.Null(result);
    }
}
