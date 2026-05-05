using Jaina.Messaging.Outbox;
using Jaina.Messaging.Outbox.EfCore;
using Jaina.Testing.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Jaina.Messaging.Outbox.EfCore.IntegrationTests;

public sealed class OutboxDb : DbContext
{
    public OutboxDb(DbContextOptions<OutboxDb> options) : base(options) { }
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyJainaOutbox();
}

/// <summary>
/// Spins one Postgres 16 container per test class. Applies the Outbox EF schema once on
/// startup via EnsureCreated so individual tests get a clean table by truncating before each.
/// </summary>
public sealed class OutboxPostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = JainaContainers.Postgres("outbox_it");
    public IDbContextFactory<OutboxDb> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContextFactory<OutboxDb>(o => o.UseNpgsql(Container.GetConnectionString()));
        var sp = services.BuildServiceProvider();
        Factory = sp.GetRequiredService<IDbContextFactory<OutboxDb>>();

        await using var ctx = await Factory.CreateDbContextAsync();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await Container.DisposeAsync();

    /// <summary>Test helper — truncate the outbox table so each test starts clean.</summary>
    public async Task ResetAsync()
    {
        await using var ctx = await Factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync("TRUNCATE jaina_outbox_messages");
    }
}
