---
title: "Resilience pipelines: 5 Polly patterns in 3 lines"
date: 2026-05-05
tags: [resilience, polly, microservices]
reading_time: "~5 min"
sample: samples/Jaina.Samples.WebApi/Program.cs
---

# Resilience pipelines: 5 Polly patterns in 3 lines

## The Story

Black Friday 11:30 PM. The product page is rock-solid — except it isn't. Your `GET /products/:id` is fast 99.5% of the time. The 0.5% it isn't, the upstream pricing service times out, the page returns 500, and the customer bounces. By 1 AM your gateway logs are 60% upstream-timeout-induced 500s and the dashboards are the colour of a tomato.

The fix has been the same for fifteen years: **wrap the outbound call in a retry, a timeout, and a circuit breaker**. With Polly v8 and `Microsoft.Extensions.Resilience` it's three lines — but the right *defaults* are still tricky to get right. `Jaina.Resilience` ships four named pipelines so you don't have to research them.

## Naive approach

```csharp
public async Task<Price> GetPriceAsync(int sku)
{
    var resp = await _http.GetAsync($"/prices/{sku}");
    resp.EnsureSuccessStatusCode();
    return await resp.Content.ReadFromJsonAsync<Price>() ?? throw new("empty body");
}
```

What breaks:

- The pricing service blips for 200 ms — your call times out, your customer sees 500. A single retry would have saved the request.
- Pricing service is fully down. You hammer it for 30 seconds × 50,000 customers × 3 retries each. The pricing team is now also having a Black Friday.
- Pricing service is *slow*. Your downstream connection pool fills up, your *own* service starts queuing requests, your latency p99 explodes.

## Jaina solution

```csharp
// Program.cs
builder.Services.AddJainaResilience();

// Resolve and execute — pipeline does retry + circuit-breaker + timeout
public class PricingClient(ResiliencePipelineProvider<string> pipelines, HttpClient http)
{
    public async Task<Price> GetPriceAsync(int sku, CancellationToken ct)
    {
        var pipeline = pipelines.GetPipeline(JainaResiliencePipelines.ExternalHttp);
        return await pipeline.ExecuteAsync(async token =>
        {
            var resp = await http.GetAsync($"/prices/{sku}", token);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<Price>(cancellationToken: token)
                   ?? throw new("empty body");
        }, ct);
    }
}
```

The four built-in pipelines (see [`JainaResiliencePipelines.cs`](../../src/resilience/Jaina.Resilience/JainaResiliencePipelines.cs)):

| Name | Strategies | Use for |
|---|---|---|
| `jaina.default` | retry 3× exp+jitter, 30s timeout | most outbound calls |
| `jaina.queue-publish` | retry 5× cap 15s, 60s timeout | broker publishes (RabbitMQ, SB) |
| `jaina.external-http` | retry 3×, **circuit breaker (50% / 10 req / 30s)**, 10s timeout | third-party APIs |
| `jaina.database` | retry 3× constant 50ms on `TimeoutRejectedException` only, 5s timeout | DB ops over flaky networks |

Override or add your own:

```csharp
services.AddJainaResilience(b => b.AddPipeline("hedge-reads", p => p
    .AddHedging(new HedgingStrategyOptions { MaxHedgedAttempts = 2, Delay = TimeSpan.FromMilliseconds(50) })));
```

## Happy path

```bash
$ curl http://localhost:5000/api/resilience/flaky
{"attempts":1,"ok":true}
```

One attempt, success. The pipeline added zero latency.

## Error scenarios

### 1. Transient blip — first attempt fails, retry succeeds

```bash
$ curl 'http://localhost:5000/api/resilience/flaky?fail=true'
{"attempts":2,"ok":true}
```

The endpoint deliberately throws on the first attempt. The pipeline retries with ~200ms exponential backoff + jitter; the second attempt succeeds. The customer never sees the 500.

### 2. Persistent failure — circuit breaker opens

After 10 calls in a 30-second window with >50% failures, the breaker flips open and the next 15 seconds of calls **fail immediately** with `BrokenCircuitException` instead of hammering the downstream. Your latency p99 stays flat; your downstream gets a chance to recover.

```
[12:00:00] external-http call 1 — 500 in 8s (timeout)
[12:00:08] external-http call 2 — 500 in 8s (timeout)
…
[12:00:50] external-http call 11 — BrokenCircuitException (immediate)
[12:01:05] external-http call 12 — half-open: 1 probe call
[12:01:05] probe success — circuit closes, traffic flows again
```

### 3. Slow response — pipeline timeout fires

If a single attempt exceeds the 10s pipeline timeout, the inner `CancellationToken` is cancelled. Your code throws `OperationCanceledException`; the pipeline's retry policy considers it transient and tries again. After three timed-out attempts, the operation fails fast — your caller doesn't wait 30s+, they get an exception in ~30s.

### 4. Cascading failure across services

Without resilience, one slow downstream pulls your whole service down: connection pool exhausted, threads blocked, queue depth growing. The combination of **circuit breaker** (stops calling) + **timeout** (caps the wait) + **bulkhead** (caps concurrency, opt-in) keeps the slowness contained.

### 5. The retry storm

Naive retry: 3 attempts × 50,000 callers = 150,000 requests in seconds. Polly's `UseJitter = true` (default in `jaina.default`) spreads attempts over the backoff window, smoothing the spike to look like normal traffic instead of a flash flood.

## What you'd see in production

OTEL trace for a single request that retried once:

```
GET /products/42         span: api.products.get          412ms
└─ pipeline.execute      span: jaina.resilience          400ms
   ├─ attempt 1          span: http.client (failed)      198ms
   └─ attempt 2          span: http.client (success)     180ms
```

Useful metrics (auto-emitted by Polly v8 when you `AddSource("Polly")`):

- `polly.attempt.duration` — histogram, P50/P99 by pipeline name
- `polly.circuit_breaker.state` — gauge, watch for `Open`
- `polly.timeout` counter — non-zero means tighten or investigate

## Trade-offs & gotchas

- **Idempotency at the operation level**, not just transport. Retrying a `POST /payments` that already succeeded but lost the response charges the card twice. Pair with [Idempotency middleware](2026-05-04-idempotency-retry-storm.md) at the service boundary.
- **Don't double-retry**. If the inner HTTP handler also retries (some SDKs do), you compound to 9× attempts. Pin the SDK retry to off.
- **Tune timeouts to your downstream's SLO**, not arbitrary numbers. A 10s pipeline timeout on a downstream with a 200ms p99 is wasteful padding; a 1s timeout will trip on every cold start.
- **Circuit breaker thresholds are hard**. Default 50% / 10 / 30s is conservative — fine for most services. If your traffic is bursty you may need the sliding-window variant (`AddCircuitBreaker(new CircuitBreakerStrategyOptions { ... SamplingDuration = TimeSpan.FromMinutes(2) })`).

## Try it yourself

```bash
git clone https://github.com/HoangSnowy/jaina-dotnet
cd jaina-dotnet
dotnet run --project samples/Jaina.Samples.WebApi

# Happy path
curl http://localhost:5000/api/resilience/flaky

# Trigger one retry
curl 'http://localhost:5000/api/resilience/flaky?fail=true'
```

## Further reading

- Source: [`JainaResilienceBuilder.cs`](../../src/resilience/Jaina.Resilience/JainaResilienceBuilder.cs)
- Tests (4/4 covering registration, retry, custom pipeline, override): [`JainaResilienceTests.cs`](../../tests/Jaina.Resilience.Tests/JainaResilienceTests.cs)
- Companion posts: [Outbox](2026-05-04-outbox-black-friday.md), [Idempotency](2026-05-04-idempotency-retry-storm.md)
