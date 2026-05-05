---
title: "Migrating monolith → microservices, one slice at a time"
date: 2026-05-05
tags: [migration, strangler-fig, outbox, servicediscovery]
reading_time: "~7 min"
sample: samples/JainaShop/
---

# Migrating monolith → microservices, one slice at a time

## The Story

Your monolith has 800k LOC, 200 endpoints, and 4 product teams stepping on each other's deploys. Every Friday someone says "we need to break this up". Six months later, a doomed all-at-once rewrite is half-finished and nobody can ship.

The way out is the **Strangler Fig pattern**: keep the monolith running, route slice-by-slice through a new gateway, ship each slice as an independent service. The monolith shrinks until there's nothing left.

`Jaina.ServiceDiscovery` + `Jaina.Resilience` + `Jaina.Messaging.Outbox` are the three primitives that make it tractable.

## The setup

```
Browser
  ↓
Gateway (new) — Jaina.MultiTenancy + Jaina.RateLimiting
  ↓
  ├── /api/products  → Catalog (new microservice)
  ├── /api/orders    → Orders (new microservice)
  └── /api/*         → Monolith (everything else, for now)
```

Each migrated slice becomes a new upstream registered with `Jaina.ServiceDiscovery`. The gateway routes `/api/<slice>` to the new service; everything else still hits the monolith.

```csharp
// Gateway
builder.Services.AddJainaServiceDiscovery();
builder.Services.AddHttpClient("catalog",  c => c.BaseAddress = new("http://catalog"));
builder.Services.AddHttpClient("orders",   c => c.BaseAddress = new("http://orders"));
builder.Services.AddHttpClient("monolith", c => c.BaseAddress = new("http://monolith"));

app.MapGet("/api/products/{*rest}",  ForwardTo("catalog"));
app.MapPost("/api/orders/{*rest}",   ForwardTo("orders"));
app.Map("/api/{*rest}",              ForwardTo("monolith"));   // catch-all
```

## The dual-write problem during migration

The monolith owns the `Orders` table. The new Orders service owns its own DB. During migration both are alive — you can't atomically write to both. Race conditions and lost updates eat your weekend.

**Outbox** is the fix. The new Orders service writes its changes to its own DB **and** an outbox row in one transaction. A relay drains the outbox to the monolith via HTTP (or a broker). The monolith stays the source of truth for read-heavy legacy reports until it's switched off.

```csharp
// New Orders service
public async Task<Result> PlaceAsync(OrderRequest req, CancellationToken ct)
{
    var order = new Order(req.Sku, req.Quantity);
    _db.Orders.Add(order);

    // Same transaction → committed atomically
    await _outbox.EnqueueAsync(new OrderCreated(order.Id, req.Sku, req.Quantity),
        destination: "monolith.legacy.orders");
    await _db.SaveChangesAsync(ct);
    return Result.Ok();
}

// Relay's IOutboxDispatcher posts to the monolith's import endpoint
public sealed class MonolithImportDispatcher : IOutboxDispatcher
{
    public async Task DispatchAsync(OutboxMessage msg, CancellationToken ct)
    {
        await _http.PostAsJsonAsync("/legacy/orders/import", msg.Payload, ct);
    }
}
```

The monolith doesn't notice the migration; it just receives "imported" orders that look identical to the ones it used to write itself. Source: [`Jaina.Messaging.Outbox`](../../src/messaging/Jaina.Messaging.Outbox/).

## Migration sequence (one slice)

1. **Identify a vertical slice** — feature with a clean boundary (Catalog, Orders, Identity).
2. **Stand up the new service** — new DB, new code, new tests. **Don't** call the monolith from it.
3. **Backfill** — copy the slice's data from the monolith DB to the new DB once.
4. **Forward write traffic** — gateway routes the slice's POST/PUT/DELETE to the new service. **Reads** still go to the monolith via cache for now.
5. **Replicate writes back** — outbox-publishes new writes back to the monolith so legacy reports keep working.
6. **Switch reads** — gateway routes the slice's GETs to the new service.
7. **Stop replicating** — once nothing in the monolith reads the slice's data, drop the legacy table.

## Happy path

Day 0:
```
  POST /api/products  → monolith
  GET  /api/products  → monolith
```

Day 7 (Catalog migrated, dual-write active):
```
  POST /api/products  → catalog (new) — writes its DB + outbox to monolith
  GET  /api/products  → monolith (still reads)
```

Day 14 (read traffic switched):
```
  POST /api/products  → catalog
  GET  /api/products  → catalog
  Outbox still replicates to monolith for legacy reports
```

Day 30 (legacy reports rewritten, outbox dropped):
```
  POST /api/products  → catalog
  GET  /api/products  → catalog
  Monolith.Products table → DROPPED
```

Each step is a small, reversible change. No big-bang.

## Error scenarios

### 1. New service goes down mid-migration

`Jaina.Resilience` retry + circuit breaker on the gateway HttpClient. If breaker opens, fall back to the monolith for that slice (configurable). Customer sees temporary degradation, not 500s.

```csharp
app.MapPost("/api/orders/{*rest}", async (HttpRequest req, IHttpClientFactory http, ResiliencePipelineProvider<string> pipelines) =>
{
    var pipeline = pipelines.GetPipeline(JainaResiliencePipelines.ExternalHttp);
    try
    {
        return await pipeline.ExecuteAsync(_ => Forward(req, http.CreateClient("orders")));
    }
    catch (BrokenCircuitException)
    {
        return await Forward(req, http.CreateClient("monolith"));   // fallback
    }
});
```

### 2. Outbox replication backs up — monolith out of date

The outbox table grows. Alert on `jaina.outbox.pending > 1000` for > 5 min. Investigate what's blocking the relay (broker down, monolith import endpoint slow).

### 3. Schema drift between monolith and new service

The new service evolves its schema; the monolith still has the old shape. Outbox payload must be **the monolith's shape** (or transformable to it). Use a versioned message: `OrderCreated.v1` for monolith compatibility, `OrderCreated.v2` for downstream new consumers.

### 4. The "tenant id" wasn't in the monolith

Monolith has implicit single-tenant; new service is multi-tenant from day one. During migration, hardcode `tenant=default` for monolith-replicated writes. New traffic carries the real tenant. Plan to backfill the tenant column on the legacy table before turning off the monolith.

### 5. Reads return inconsistent data during cutover

For ~30 seconds after switching read traffic, the cache might still serve a stale view. Use a versioned cache key (`catalog:v2:product:42`) and bump the version when you cut over.

### 6. Rollback

A bad slice → revert the gateway routing rule (one config change, redeployed in minutes). The new service is dormant; the monolith resumes serving the slice. Outbox lets you replay missed writes after fixing.

## What you'd see in production

OTEL trace during migration:

```
POST /api/products            jaina.gateway.forward         95ms
  ├─ http.client (catalog)    (new service)                 80ms
  │  ├─ db.savechanges                                      40ms
  │  └─ jaina.outbox.enqueue                                 1ms
  └─ (returns 201)

  ↓ ~500ms later

jaina.outbox.dispatch         (replication to monolith)     65ms
  └─ http.client (monolith)   POST /legacy/products/import  60ms
```

Two services see the same write. No coordination beyond the outbox.

## Trade-offs & gotchas

- **Don't migrate a slice with no clear boundary.** "Auth" looks easy; in practice it touches every endpoint. Start with a low-coupling slice.
- **Backfill is harder than the migration**. Plan it carefully — it's the part that surprises teams.
- **Don't carry the monolith's schema forward.** This is the chance to fix bad designs. But ship the migration first; refactor the new service's schema later.
- **Two systems of record is worse than one.** Get out of the dual-write phase as fast as possible. Don't let "the migration" become permanent.

## Try it yourself

The `JainaShop` sample is what a finished slice-set looks like (5 services, no monolith). To simulate migration, add a `JainaShop.Monolith` project that owns a copy of the products + orders tables, register it as `http://monolith` in `appsettings.json`, and route `/api/legacy/*` to it. Watch the outbox dispatch from the new services replicate writes back.

## Further reading

- [Strangler Fig pattern (Martin Fowler)](https://martinfowler.com/bliki/StranglerFigApplication.html)
- Source: [`Jaina.ServiceDiscovery`](../../src/servicediscovery/Jaina.ServiceDiscovery/), [`Jaina.Resilience`](../../src/resilience/Jaina.Resilience/), [`Jaina.Messaging.Outbox`](../../src/messaging/Jaina.Messaging.Outbox/)
- Companion posts: [Outbox](2026-05-04-outbox-black-friday.md), [Service discovery](2026-05-05-service-discovery.md), [Resilience](2026-05-05-resilience-pipelines.md)
