using BenchmarkDotNet.Attributes;
using Jaina.Messaging.Outbox;
using Jaina.Messaging.Outbox.InMemory;

namespace Jaina.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(invocationCount: 10_000, warmupCount: 3)]
public class OutboxBench
{
    private IOutbox _outbox = null!;
    private static readonly OrderPlaced Sample = new(Guid.NewGuid(), "WIDGET", 3);

    [GlobalSetup]
    public void Setup()
    {
        var store = new InMemoryOutboxStore();
        _outbox = new InMemoryOutbox(store);
    }

    [Benchmark(Description = "EnqueueAsync — InMemory store")]
    public Task EnqueueInMemory() =>
        _outbox.EnqueueAsync(Sample, destination: "orders.events");

    private sealed record OrderPlaced(Guid OrderId, string Sku, int Quantity);
}
