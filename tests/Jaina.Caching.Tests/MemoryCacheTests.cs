using Jaina.Caching;
using Jaina.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Caching.Tests;

public class MemoryCacheTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ICache _cache;

    public MemoryCacheTests()
    {
        var services = new ServiceCollection();
        services.AddJainaMemoryCache();
        _provider = services.BuildServiceProvider();
        _cache = _provider.GetRequiredService<ICache>();
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void Set_ThenGet_ReturnsValue()
    {
        // Arrange / Act
        _cache.Set("key1", "value1", TimeSpan.FromMinutes(5));
        var result = _cache.Get<string>("key1");

        // Assert
        Assert.Equal("value1", result);
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        // Act
        var result = _cache.Get<string>("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Remove_ExistingKey_MakesValueUnavailable()
    {
        // Arrange
        _cache.Set("key2", "value2", TimeSpan.FromMinutes(5));

        // Act
        _cache.Remove("key2");
        var result = _cache.Get<string>("key2");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsValue()
    {
        // Arrange / Act
        await _cache.SetAsync("async-key", "async-value", TimeSpan.FromMinutes(5));
        var result = await _cache.GetAsync<string>("async-key");

        // Assert
        Assert.Equal("async-value", result);
    }

    [Fact]
    public async Task RemoveAsync_ExistingKey_MakesValueUnavailable()
    {
        // Arrange
        await _cache.SetAsync("del-key", "del-value", TimeSpan.FromMinutes(5));

        // Act
        await _cache.RemoveAsync("del-key");
        var result = await _cache.GetAsync<string>("del-key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_WithOptions_ReturnsValue()
    {
        // Arrange
        var options = CacheEntryOptions.AbsoluteRelative(TimeSpan.FromMinutes(10));

        // Act
        _cache.Set("opts-key", "opts-value", options);
        var result = _cache.Get<string>("opts-key");

        // Assert
        Assert.Equal("opts-value", result);
    }

    [Fact]
    public void IsDistributed_ForMemoryCache_ReturnsFalse()
    {
        // Act / Assert
        Assert.False(_cache.IsDistributed);
    }

    [Fact]
    public void Get_WithFactory_CallsFactoryOnCacheMiss()
    {
        // Arrange
        var callCount = 0;

        // Act
        var result = _cache.Get<string>("factory-key", TimeSpan.FromMinutes(5), () =>
        {
            callCount++;
            return "factory-value";
        });

        // Assert
        Assert.Equal("factory-value", result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Get_WithFactory_DoesNotCallFactoryOnCacheHit()
    {
        // Arrange
        _cache.Set("hit-key", "cached", TimeSpan.FromMinutes(5));
        var callCount = 0;

        // Act
        var result = _cache.Get<string>("hit-key", TimeSpan.FromMinutes(5), () =>
        {
            callCount++;
            return "from-factory";
        });

        // Assert
        Assert.Equal("cached", result);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Set_ComplexObject_RoundTrips()
    {
        // Arrange
        var record = new TestRecord(42, "Jaina");

        // Act
        _cache.Set("record-key", record, TimeSpan.FromMinutes(5));
        var result = _cache.Get<TestRecord>("record-key");

        // Assert
        Assert.Equal(record, result);
    }

    private record TestRecord(int Id, string Name);
}
