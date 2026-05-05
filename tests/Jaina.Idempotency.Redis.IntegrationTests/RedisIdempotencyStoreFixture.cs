using Jaina.Testing.Containers;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Jaina.Idempotency.Redis.IntegrationTests;

/// <summary>
/// xUnit class fixture that spins a real Redis container once per test class. The docker
/// image pull runs only on the first <c>InitializeAsync</c>; subsequent tests share the
/// running container.
/// </summary>
public sealed class RedisIdempotencyStoreFixture : IAsyncLifetime
{
    public RedisContainer Container { get; } = JainaContainers.Redis();
    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        Connection = await ConnectionMultiplexer.ConnectAsync(Container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync();
        await Container.DisposeAsync();
    }
}
