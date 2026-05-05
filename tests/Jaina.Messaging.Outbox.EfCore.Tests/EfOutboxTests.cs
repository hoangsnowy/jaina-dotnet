using Jaina.Messaging.Outbox;
using Jaina.Messaging.Outbox.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jaina.Messaging.Outbox.EfCore.Tests;

public class EfOutboxTests
{
    [Fact]
    public async Task EnqueueAsync_AddsToDbContextChangeTracker_WithoutSaving()
    {
        // Arrange
        await using var ctx = NewContext("outbox-enqueue-1");
        var outbox = new EfOutbox<TestDbContext>(ctx);

        // Act
        await outbox.EnqueueAsync(new OrderPlaced(7, "X"));

        // Assert — entity is tracked but not yet persisted
        var added = ctx.ChangeTracker.Entries<OutboxMessage>().ToList();
        Assert.Single(added);
        Assert.Equal(EntityState.Added, added[0].State);

        var dbCount = await ctx.Set<OutboxMessage>().CountAsync();
        Assert.Equal(0, dbCount);  // not persisted until SaveChanges
    }

    [Fact]
    public async Task EnqueueThenSaveChanges_PersistsAtomically()
    {
        // Arrange
        await using var ctx = NewContext("outbox-enqueue-2");
        var outbox = new EfOutbox<TestDbContext>(ctx);

        // Act — caller's SaveChangesAsync persists everything in one transaction
        await outbox.EnqueueAsync(new OrderPlaced(1, "A"));
        await outbox.EnqueueAsync(new OrderPlaced(2, "B"));
        await ctx.SaveChangesAsync();

        // Assert
        var rows = await ctx.Set<OutboxMessage>().AsNoTracking().ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(typeof(OrderPlaced).FullName, rows[0].PayloadType);
    }

    [Fact]
    public async Task ClaimBatch_ReturnsPendingMessages_OldestFirst()
    {
        // Arrange — seed two messages with distinct ScheduledFor
        var (factory, db) = await SeedAsync("outbox-claim-1", async ctx =>
        {
            ctx.Set<OutboxMessage>().AddRange(
                new OutboxMessage { PayloadType = "T", Payload = "{}", ScheduledFor = DateTimeOffset.UtcNow.AddSeconds(-10) },
                new OutboxMessage { PayloadType = "T", Payload = "{}", ScheduledFor = DateTimeOffset.UtcNow.AddSeconds(-5) });
            await ctx.SaveChangesAsync();
        });

        var store = new EfOutboxStore<TestDbContext>(factory);

        // Act
        var batch = await store.ClaimBatchAsync(10);

        // Assert
        Assert.Equal(2, batch.Count);
        Assert.True(batch[0].ScheduledFor < batch[1].ScheduledFor);
    }

    [Fact]
    public async Task ClaimBatch_ExcludesProcessedAndFutureMessages()
    {
        var (factory, _) = await SeedAsync("outbox-claim-2", async ctx =>
        {
            ctx.Set<OutboxMessage>().AddRange(
                new OutboxMessage { PayloadType = "T", Payload = "{}", ProcessedAt = DateTimeOffset.UtcNow },
                new OutboxMessage { PayloadType = "T", Payload = "{}", ScheduledFor = DateTimeOffset.UtcNow.AddMinutes(5) },
                new OutboxMessage { PayloadType = "T", Payload = "{}" });
            await ctx.SaveChangesAsync();
        });

        var store = new EfOutboxStore<TestDbContext>(factory);

        var batch = await store.ClaimBatchAsync(10);

        // Only the third (pending and due now) is claimed
        Assert.Single(batch);
    }

    [Fact]
    public async Task MarkProcessed_SetsProcessedAt()
    {
        var (factory, _) = await SeedAsync("outbox-mark-ok", async ctx =>
        {
            ctx.Set<OutboxMessage>().Add(new OutboxMessage { Id = TestId, PayloadType = "T", Payload = "{}" });
            await ctx.SaveChangesAsync();
        });

        var store = new EfOutboxStore<TestDbContext>(factory);
        await store.MarkProcessedAsync(TestId);

        await using var ctx = factory.CreateDbContext();
        var msg = await ctx.Set<OutboxMessage>().AsNoTracking().FirstAsync(m => m.Id == TestId);
        Assert.NotNull(msg.ProcessedAt);
    }

    [Fact]
    public async Task MarkFailed_IncrementsAttempts_AndReschedules()
    {
        var (factory, _) = await SeedAsync("outbox-mark-fail", async ctx =>
        {
            ctx.Set<OutboxMessage>().Add(new OutboxMessage { Id = TestId, PayloadType = "T", Payload = "{}" });
            await ctx.SaveChangesAsync();
        });

        var store = new EfOutboxStore<TestDbContext>(factory);
        var nextAt = DateTimeOffset.UtcNow.AddMinutes(2);
        await store.MarkFailedAsync(TestId, "broker is down", nextAt);

        await using var ctx = factory.CreateDbContext();
        var msg = await ctx.Set<OutboxMessage>().AsNoTracking().FirstAsync(m => m.Id == TestId);
        Assert.Null(msg.ProcessedAt);
        Assert.Equal(1, msg.Attempts);
        Assert.Equal("broker is down", msg.LastError);
        // ScheduledFor moved into the future
        Assert.True(msg.ScheduledFor > DateTimeOffset.UtcNow);
    }

    // ── helpers ────────────────────────────────────────────────────────

    private static readonly Guid TestId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TestDbContext NewContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TestDbContext(options);
    }

    private static async Task<(IDbContextFactory<TestDbContext> factory, TestDbContext seed)> SeedAsync(
        string dbName, Func<TestDbContext, Task> seed)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IDbContextFactory<TestDbContext>>();

        await using var ctx = factory.CreateDbContext();
        await seed(ctx);
        return (factory, ctx);
    }

    private sealed record OrderPlaced(int OrderId, string Sku);

    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // The InMemory provider doesn't run table-mapping calls (ToTable / HasDatabaseName /
            // value converters that use SQL), so map only what's portable.
            modelBuilder.Entity<OutboxMessage>(b =>
            {
                b.HasKey(m => m.Id);
                b.Ignore(m => m.Headers);  // skip JSON conversion for the InMemory provider
            });
        }
    }
}
