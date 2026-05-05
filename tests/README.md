# Jaina Tests

Two suites, separated by what they need to run.

## Unit tests — `*.Tests`

- No external dependencies (no Docker, no network, no DB)
- Run in-process with in-memory providers (`Microsoft.Extensions.Caching.Memory`, `EF Core InMemory`, etc.)
- Run on **every commit** in CI; sub-second per test
- Pattern: `tests/Jaina.<Module>.Tests/`

Example: [Jaina.Idempotency.Tests](Jaina.Idempotency.Tests/) covers the abstraction + in-memory store + AspNetCore middleware via `DefaultHttpContext`.

## Integration tests — `*.IntegrationTests`

- Require **Docker** (uses Testcontainers for Postgres / Redis / RabbitMQ / Azurite)
- Cover the real provider implementations (`*.EfCore`, `*.Redis`, `*.RabbitMQ`, `*.AzureBlob`, ...)
- Run in CI with the docker-enabled job; locally optional
- Pattern: `tests/Jaina.<Module>.<Provider>.IntegrationTests/`
- Each fixture comes from `Jaina.Testing.Containers.JainaContainers.*`

Example: [Jaina.Idempotency.Redis.IntegrationTests](Jaina.Idempotency.Redis.IntegrationTests/) spins up a real Redis container for the lifetime of the class via `IAsyncLifetime` and exercises the production code path end-to-end.

## Running

```bash
# Unit only — quick iteration
dotnet test --filter "FullyQualifiedName!~IntegrationTests"

# Integration only — needs Docker running
dotnet test --filter "FullyQualifiedName~IntegrationTests"

# Everything
dotnet test
```

## Conventions

- Unit tests use `Assert.*` from xUnit only — **do not** add FluentAssertions / Shouldly (commercial license)
- AAA inline comments (`// Arrange / Act / Assert`)
- One concern per `[Fact]`; use `[Theory] + [InlineData]` for parameterised cases
- Test method name: `MethodName_Condition_ExpectedBehavior`
- Integration tests share a fixture per container (xUnit `IClassFixture<T>`) so the docker pull happens once per class
