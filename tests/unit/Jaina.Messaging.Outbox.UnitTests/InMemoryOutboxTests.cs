using Jaina.Messaging.Outbox;
using Jaina.Messaging.Outbox.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jaina.Messaging.Outbox.UnitTests;

public class InMemoryOutboxTests
{
    [Fact]
    public async Task EnqueueThenClaim_ReturnsMessage()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var outbox = new InMemoryOutbox(store);

        // Act
        await outbox.EnqueueAsync(new OrderPlaced(42, "ABC"));
        var batch = await store.ClaimBatchAsync(10);

        // Assert
        Assert.Single(batch);
        Assert.Equal(typeof(OrderPlaced).FullName, batch[0].PayloadType);
        Assert.Contains("\"OrderId\":42", batch[0].Payload);
    }

    [Fact]
    public async Task ClaimBatch_IsExclusive_SecondClaim_DoesNotReturnSameMessage()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        await new InMemoryOutbox(store).EnqueueAsync(new OrderPlaced(1, ""));

        // Act
        var first = await store.ClaimBatchAsync(10);
        var second = await store.ClaimBatchAsync(10);

        // Assert
        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public async Task ScheduledFor_InFuture_NotClaimed()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var outbox = new InMemoryOutbox(store);
        await outbox.EnqueueAsync(new OrderPlaced(1, ""), scheduledFor: DateTimeOffset.UtcNow.AddMinutes(5));

        // Act
        var batch = await store.ClaimBatchAsync(10);

        // Assert
        Assert.Empty(batch);
    }

    [Fact]
    public async Task Relay_DispatchSuccess_MarksProcessed()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var outbox = new InMemoryOutbox(store);
        var dispatcher = new RecordingDispatcher();
        var relay = new OutboxRelay(store, dispatcher,
            Options.Create(new OutboxOptions { BatchSize = 10 }),
            NullLogger<OutboxRelay>.Instance);

        await outbox.EnqueueAsync(new OrderPlaced(7, "X"));

        // Act
        await relay.ProcessOnceAsync();

        // Assert — dispatched once, message marked processed
        Assert.Single(dispatcher.Calls);
        var snap = store.Snapshot();
        Assert.Single(snap);
        Assert.NotNull(snap.First().ProcessedAt);
    }

    [Fact]
    public async Task Relay_DispatchFails_MarksFailedAndReschedules()
    {
        // Arrange
        var store = new InMemoryOutboxStore();
        var outbox = new InMemoryOutbox(store);
        var dispatcher = new ThrowingDispatcher("broker is down");
        var relay = new OutboxRelay(store, dispatcher,
            Options.Create(new OutboxOptions
            {
                BatchSize = 10,
                InitialBackoff = TimeSpan.FromMinutes(1),
            }),
            NullLogger<OutboxRelay>.Instance);

        await outbox.EnqueueAsync(new OrderPlaced(9, "Y"));

        // Act
        await relay.ProcessOnceAsync();

        // Assert — message attempts incremented, scheduled in the future, last error captured
        var msg = store.Snapshot().Single();
        Assert.Null(msg.ProcessedAt);
        Assert.Equal(1, msg.Attempts);
        Assert.Equal("broker is down", msg.LastError);
        Assert.True(msg.ScheduledFor > DateTimeOffset.UtcNow);
    }

    private sealed record OrderPlaced(int OrderId, string Sku);

    private sealed class RecordingDispatcher : IOutboxDispatcher
    {
        public List<OutboxMessage> Calls { get; } = new();
        public Task DispatchAsync(OutboxMessage message, CancellationToken ct = default)
        {
            Calls.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDispatcher : IOutboxDispatcher
    {
        private readonly string _error;
        public ThrowingDispatcher(string error) => _error = error;
        public Task DispatchAsync(OutboxMessage message, CancellationToken ct = default) =>
            throw new InvalidOperationException(_error);
    }
}
