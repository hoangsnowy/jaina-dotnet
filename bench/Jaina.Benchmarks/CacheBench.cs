using BenchmarkDotNet.Attributes;
using Jaina.Caching;
using Microsoft.Extensions.Caching.Memory;
using JainaMemoryCache = Jaina.Caching.Memory.MemoryCache;
using MsMemoryCache = Microsoft.Extensions.Caching.Memory.MemoryCache;

namespace Jaina.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(invocationCount: 10_000, warmupCount: 3)]
public class CacheBench
{
    private ICache _cache = null!;
    private const string Key = "bench-key";
    private static readonly object Value = new { Id = 42, Name = "Widget" };

    [GlobalSetup]
    public void Setup()
    {
        _cache = new JainaMemoryCache(new MsMemoryCache(new MemoryCacheOptions()));
        _cache.Set(Key, Value, TimeSpan.FromMinutes(10));
    }

    [Benchmark(Description = "Get<object> on hit")]
    public object? GetHit() => _cache.Get<object>(Key);

    [Benchmark(Description = "Get<object> on miss")]
    public object? GetMiss() => _cache.Get<object>("missing");

    [Benchmark(Description = "Set<object>")]
    public void Set() => _cache.Set("k", Value, TimeSpan.FromMinutes(10));
}
