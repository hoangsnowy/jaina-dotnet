using Jaina.Messaging.Outbox;
using Jaina.Messaging.Outbox.EfCore;
using Microsoft.EntityFrameworkCore;

namespace Jaina.Messaging.Outbox.EfCore.IntegrationTests;

[Collection("postgres-outbox")]
public class EfOutboxIntegrationTests : IClassFixture<OutboxPostgresFixture>, IAsyncLifetime
{
    private readonly OutboxPostgresFixture _fx;

    public EfOutboxIntegrationTests(OutboxPostgresFixture fx) => _fx = fx;

    public Task InitializeAsync() => _fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EnqueueViaContext_AndSaveChanges_PersistsToPostgres()
    {
        // Arrange
        await using var ctx = await _fx.Factory.CreateDbContextAsync();
        var outbox = new EfOutbox<OutboxDb>(ctx);

        // Act
        await outbox.EnqueueAsync(new { OrderId = 7, Sku = "X" }, destination: "orders.events");
        await ctx.SaveChangesAsync();

        // Assert
        await using var verify = await _fx.Factory.CreateDbContextAsync();
        var rows = await verify.Set<OutboxMessage>().AsNoTracking().ToListAsync();
        Assert.Single(rows);
        Assert.Equal("orders.events", rows[0].Destination);
    }

    [Fact]
    public async Task ClaimBatch_ReturnsPending_OldestFirst()
    {
        // Arrange — seed two messages directly
        var store = new EfOutboxStore<OutboxDb>(_fx.Factory);
        await store.AddAsync(new OutboxMessage
        {
            PayloadType = "T", Payload = "{}",
            ScheduledFor = DateTimeOffset.UtcNow.AddSeconds(-10),
        });
        await store.AddAsync(new OutboxMessage
        {
            PayloadType = "T", Payload = "{}",
            ScheduledFor = DateTimeOffset.UtcNow.AddSeconds(-5),
        });

        // Act
        var batch = await store.ClaimBatchAsync(10);

        // Assert
        Assert.Equal(2, batch.Count);
        Assert.True(batch[0].ScheduledFor < batch[1].ScheduledFor);
    }

    [Fact]
    public async Task MarkProcessed_And_MarkFailed_RoundTripThroughPostgres()
    {
        // Arrange
        var store = new EfOutboxStore<OutboxDb>(_fx.Factory);
        var ok = new OutboxMessage { Id = Guid.NewGuid(), PayloadType = "T", Payload = "{}" };
        var fail = new OutboxMessage { Id = Guid.NewGuid(), PayloadType = "T", Payload = "{}" };
        await store.AddAsync(ok);
        await store.AddAsync(fail);

        var nextAt = DateTimeOffset.UtcNow.AddMinutes(2);

        // Act
        await store.MarkProcessedAsync(ok.Id);
        await store.MarkFailedAsync(fail.Id, "broker down", nextAt);

        // Assert — re-read both
        await using var verify = await _fx.Factory.CreateDbContextAsync();
        var okMsg = await verify.Set<OutboxMessage>().AsNoTracking().FirstAsync(m => m.Id == ok.Id);
        var failMsg = await verify.Set<OutboxMessage>().AsNoTracking().FirstAsync(m => m.Id == fail.Id);

        Assert.NotNull(okMsg.ProcessedAt);
        Assert.Null(failMsg.ProcessedAt);
        Assert.Equal(1, failMsg.Attempts);
        Assert.Equal("broker down", failMsg.LastError);
    }
}
