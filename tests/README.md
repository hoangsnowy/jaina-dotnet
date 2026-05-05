# Jaina Tests

Three suites, separated by what they need to run. Each lives under its own folder and is selected by a solution filter (`.slnf`) at the repo root.

## `tests/unit/` — fast, no external dependencies

- Run on **every commit** in CI (`unit` job, matrix of net8 / net9 / net10)
- No Docker, no network, no real DB; in-memory providers only
- Pattern: `tests/unit/Jaina.<Module>.UnitTests/`
- Solution filter: [`Jaina.UnitTests.slnf`](../Jaina.UnitTests.slnf)

```bash
dotnet test Jaina.UnitTests.slnf
```

## `tests/integration/` — real provider, Docker required

- Run after unit passes in CI (`integration` job, net10 only)
- **Docker required** — Testcontainers spins up Postgres / Redis / RabbitMQ / Azurite per fixture
- Provider implementations exercised end-to-end against a real backend (no InMemory shortcuts)
- Pattern: `tests/integration/Jaina.<Module>.<Provider>.IntegrationTests/`
- Solution filter: [`Jaina.IntegrationTests.slnf`](../Jaina.IntegrationTests.slnf)

```bash
docker info >/dev/null   # confirm Docker is running
dotnet test Jaina.IntegrationTests.slnf
```

Currently shipped:

- `Jaina.Idempotency.Redis.IntegrationTests` (Testcontainers Redis)
- `Jaina.Messaging.Outbox.EfCore.IntegrationTests` (Testcontainers Postgres)

More land per provider as they're added.

## `tests/benchmarks/` — performance regression detection

- Run on demand, never in PR gate
- BenchmarkDotNet, `[MemoryDiagnoser]`, `[SimpleJob(invocationCount: 10_000, warmupCount: 3)]`
- Pattern: `tests/benchmarks/Jaina.Benchmarks/`
- Solution filter: [`Jaina.Benchmarks.slnf`](../Jaina.Benchmarks.slnf)

```bash
dotnet run -c Release --project tests/benchmarks/Jaina.Benchmarks
dotnet run -c Release --project tests/benchmarks/Jaina.Benchmarks -- --filter "*CacheBench*"
```

## Why no regression suite?

FlowOrchestrator (the sibling project that inspired this layout) ships a `tests/regression/` suite for timing- and concurrency-stress scenarios — it owns its own scheduler/dispatcher state. Jaina is a framework library: most concurrency primitives are delegated to `Microsoft.Extensions.*` and the underlying brokers, so the equivalent stress lives in `tests/integration/` against real providers.

If you have a deterministic bug repro in Jaina's surface (Outbox relay double-claim, Saga concurrent run, etc.), add it to the matching unit or integration suite — that's where it'll catch attention.

## Conventions

- xUnit only. `Assert.*` from xUnit (no FluentAssertions / Shouldly — commercial license).
- AAA inline comments (`// Arrange / Act / Assert`).
- One concern per `[Fact]`; use `[Theory] + [InlineData]` for parameterised cases.
- Method name: `MethodName_Condition_ExpectedBehavior`.
- Integration tests: one fixture per container, shared with `IClassFixture<T>` so the docker pull happens once per class.

## Solution filters at a glance

| Filter | Includes | When |
|---|---|---|
| `Jaina.Libraries.slnf` | All `src/**/*.csproj` only | `dotnet pack` / publish |
| `Jaina.UnitTests.slnf` | `src/**/*.csproj` + `tests/unit/**/*.csproj` | every PR (unit job, TFM matrix) |
| `Jaina.IntegrationTests.slnf` | `src/**/*.csproj` + `tests/integration/**/*.csproj` | every PR (integration job, net10 + Docker) |
| `Jaina.Benchmarks.slnf` | `src/**/*.csproj` + `tests/benchmarks/Jaina.Benchmarks` | on demand |
