---
title: "Reading OTEL traces like a novel: distributed bug detective work"
date: 2026-05-05
tags: [observability, opentelemetry, distributed-tracing]
reading_time: "~7 min"
sample: src/observability/Jaina.Observability/Telemetry/
---

# Reading OTEL traces like a novel: distributed bug detective work

## The Story

Friday 4:55 PM. Customer reports order #4827 was charged twice. You start at the orders DB — one row. Logs show one POST. So where's the second charge? You jump to the payments service logs — two `Charge` calls, 800ms apart. Was there a retry? Why? The on-call is helpless without the full distributed trace.

Tracing turns "where did the request go?" into a tree you can read top-down. **OpenTelemetry** is the standard. **`Jaina.Observability`** ships the conventions every Jaina provider uses so the tree is consistent.

## The conventions

Source: [`Telemetry/JainaActivitySource.cs`](../../src/observability/Jaina.Observability/Telemetry/JainaActivitySource.cs), [`JainaMeter.cs`](../../src/observability/Jaina.Observability/Telemetry/JainaMeter.cs), [`TagConventions.cs`](../../src/observability/Jaina.Observability/Telemetry/TagConventions.cs).

- **One source name**: `Jaina`. Subscribe in OTEL setup with `otel.AddSource(JainaActivitySource.Name)`.
- **Span name format**: `jaina.<module>.<operation>` — e.g. `jaina.cache.get`, `jaina.outbox.dispatch`, `jaina.saga.run`.
- **Standard tags**: `jaina.tenant.id`, `jaina.correlation.id`, `jaina.user.id`, `jaina.cache.hit`, `jaina.message.id`, `jaina.outbox.attempt`, `jaina.idempotency.key`, etc.

Same names everywhere → dashboards just work.

## Setup

```csharp
// In ServiceDefaults
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource(JainaActivitySource.Name)            // every Jaina span
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddMeter(JainaMeter.Name)                      // every Jaina counter / histogram
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation())
    .UseOtlpExporter();   // ships to Aspire dashboard / Tempo / Jaeger / etc.
```

Provider code (e.g. inside `MemoryCache.Get`):

```csharp
using var span = JainaActivitySource.StartSpan("cache", "get");
span?.SetTag(TagConventions.CacheKey, key);
span?.SetTag(TagConventions.CacheHit, hit);
```

## Reading a real trace

The double-charge case from the story:

```
POST /api/orders                      jaina.orders.place           250ms
  jaina.tenant.id      = "acme"
  jaina.correlation.id = "abc-123"
  ├─ db.savechanges                                                 22ms
  ├─ jaina.outbox.enqueue                                            1ms
  │  └─ jaina.message.id = "m-7f"
  └─ (returns 201)

  ↓ relay tick ~500ms later

jaina.outbox.relay.tick               jaina.outbox.relay           45ms
  └─ jaina.outbox.dispatch            jaina.outbox.dispatch        40ms
     ├─ jaina.message.id   = "m-7f"
     ├─ jaina.outbox.attempt = 1
     ├─ http.client.duration                                       38ms
     │  └─ POST payments.svc/charges -> 504 Gateway Timeout
     └─ (failure: rescheduled)

jaina.outbox.relay.tick               jaina.outbox.relay           4s 800ms
  └─ jaina.outbox.dispatch            jaina.outbox.dispatch        4s 750ms
     ├─ jaina.outbox.attempt = 2
     └─ http.client.duration                                      4s 700ms
        └─ POST payments.svc/charges -> 200 OK   ← success
```

The tree tells the whole story:

1. Order written to DB, message enqueued, 201 returned (250ms)
2. Relay tries to dispatch the message → upstream 504
3. Backoff, retry, dispatch succeeds

If the **payments service** logs show two `Charge` calls but our outbox only emitted **one** message and our trace only shows two **dispatch attempts**, then something on the payments side is the dupe — not us. We have evidence.

## Slicing dashboards

By tenant:

```
sum(rate(http_server_duration_seconds_count{...}[5m])) by (jaina_tenant_id)
```

By outbox attempt count:

```
histogram_quantile(0.99, rate(jaina_outbox_dispatch_duration_bucket[5m]))
```

Top-N tenants by error rate:

```
topk(10, sum(rate(http_server_duration_seconds_count{status_code=~"5.."}[5m])) by (jaina_tenant_id))
```

## Error scenarios

### 1. Trace context lost across `await`

Default behaviour in modern .NET preserves it. Don't capture `Activity.Current` and try to use it after a `Task.Run` without explicit propagation — that's the textbook break.

### 2. Span sampling skipped a critical span

`AlwaysOnSampler()` for dev. `TraceIdRatioBasedSampler(0.1)` for prod. Force-sample the slow ones with `ParentBasedSampler` + a custom rule (e.g. always trace requests > 1s).

### 3. Tag explosion

Tagging by `user_id` blows up cardinality. Tag by `tenant_id` (low cardinality) and put the user id in **logs**, not span tags.

### 4. Two services, same `correlation-id` but no parent span

Means one service didn't propagate the W3C Trace Context (`traceparent` header). For HTTP, `HttpClient` does it automatically. For RabbitMQ / Kafka, the producer sets `traceparent` in the message headers and the consumer reads it back into `Activity.Current` — `Jaina.Messaging.Outbox` carries this in the standard `Headers` dictionary.

### 5. Trace shows everything green but customer reports failure

Look at logs for that `correlation-id` outside the trace. Possibly an early-return before the span started, or an exception swallowed without `Activity.SetStatus(ActivityStatusCode.Error)`.

## Trade-offs & gotchas

- **Dashboards drift from code.** Pin span names / tags via `TagConventions` constants — never type literals into Grafana queries.
- **Sampling is forever**. Once a trace is dropped, it's dropped. Always-on for low-traffic services; tail sampling (with [OTEL Collector](https://opentelemetry.io/docs/collector/)) for high traffic.
- **PII in tags is permanent**. Don't tag with email, full name, payment numbers. Tag with stable opaque ids only.

## Try it yourself

```bash
dotnet run --project samples/JainaShop/JainaShop.AppHost
# Open the Aspire dashboard URL printed in console.
# Hit the gateway endpoints; watch the trace tree across services in real time.
```

## Further reading

- Source: [`JainaActivitySource.cs`](../../src/observability/Jaina.Observability/Telemetry/JainaActivitySource.cs), [`TagConventions.cs`](../../src/observability/Jaina.Observability/Telemetry/TagConventions.cs)
- Companion: [Outbox](2026-05-04-outbox-black-friday.md), [Saga](2026-05-05-saga-orchestration.md)
