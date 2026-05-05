using BenchmarkDotNet.Attributes;
using Jaina.Idempotency;
using Jaina.Idempotency.InMemory;
using Microsoft.Extensions.Caching.Memory;

namespace Jaina.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(invocationCount: 10_000, warmupCount: 3)]
public class IdempotencyBench
{
    private IIdempotencyStore _store = null!;
    private static readonly IdempotencyEntry Entry = new(201, "application/json", new byte[] { 1, 2, 3, 4 }, DateTimeOffset.UtcNow);

    [GlobalSetup]
    public async Task Setup()
    {
        _store = new InMemoryIdempotencyStore(new MemoryCache(new MemoryCacheOptions()));
        await _store.SetAsync("hit-key", Entry, TimeSpan.FromMinutes(10));
    }

    [Benchmark(Description = "Get on hit — InMemory store")]
    public Task<IdempotencyEntry?> GetHit() => _store.GetAsync("hit-key");

    [Benchmark(Description = "Get on miss — InMemory store")]
    public Task<IdempotencyEntry?> GetMiss() => _store.GetAsync("missing-key");

    [Benchmark(Description = "Set — InMemory store")]
    public Task Set() => _store.SetAsync("k", Entry, TimeSpan.FromMinutes(10));
}
