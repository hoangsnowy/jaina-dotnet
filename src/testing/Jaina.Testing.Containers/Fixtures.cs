using Testcontainers.Azurite;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Jaina.Testing.Containers;

/// <summary>
/// One-line fluent constructors for the Testcontainers used in Jaina integration tests.
/// All return started containers; caller awaits <c>StartAsync</c> if they want to defer.
/// </summary>
public static class JainaContainers
{
    /// <summary>Postgres 16 with a default database, ready for EF migrations.</summary>
    public static PostgreSqlContainer Postgres(string database = "jaina_tests") =>
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase(database)
            .Build();

    /// <summary>Redis 7 — used for Idempotency.Redis, Inbox.Redis, Saga.Redis tests.</summary>
    public static RedisContainer Redis() =>
        new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

    /// <summary>RabbitMQ 3 with management plugin — used for Messaging.RabbitMQ tests.</summary>
    public static RabbitMqContainer RabbitMq() =>
        new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management-alpine")
            .Build();

    /// <summary>Azurite — Azure Storage emulator (Blob + Files) for Storage.AzureBlob tests.</summary>
    public static AzuriteContainer Azurite() =>
        new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();
}
