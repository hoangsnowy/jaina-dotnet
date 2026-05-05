# Jaina Benchmarks

BenchmarkDotNet harness for the hot paths. Run in **Release** mode — Debug builds skew results.

## Run

```bash
# All benchmarks
dotnet run -c Release --project bench/Jaina.Benchmarks

# Filter
dotnet run -c Release --project bench/Jaina.Benchmarks -- --filter "*CacheBench*"
dotnet run -c Release --project bench/Jaina.Benchmarks -- --filter "*Idempotency*"

# List available
dotnet run -c Release --project bench/Jaina.Benchmarks -- --list flat
```

## Suites

| Class | What it measures |
|---|---|
| `CacheBench` | `ICache.Get` (hit + miss) and `Set` against `Memory` provider |
| `OutboxBench` | `IOutbox.EnqueueAsync` against the in-memory store (no DB I/O) |
| `IdempotencyBench` | `IIdempotencyStore` `Get` (hit + miss) + `Set` against in-memory provider |

## Conventions

- `[MemoryDiagnoser]` on every class — allocations matter as much as time
- `[SimpleJob(invocationCount: 10_000, warmupCount: 3)]` for fast feedback in CI
- One concern per class
- No I/O — these benchmarks measure framework overhead, not network / disk

## CI

Add to a non-blocking CI job:

```yaml
- run: dotnet run -c Release --project bench/Jaina.Benchmarks -- --filter "*"
```

Track results over time to catch perf regressions before they ship.
