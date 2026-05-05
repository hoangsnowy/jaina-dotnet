---
title: "From hello-world to Black Friday: building a production Orders service with Jaina"
date: 2026-05-05
tags: [tutorial, ebook, idempotency, outbox, saga, observability]
reading_time: "~50 min"
audience: "intermediate .NET devs comfortable with ASP.NET Core minimal APIs and EF Core"
sample: samples/JainaShop/JainaShop.Orders/
---

# From hello-world to Black Friday: building a production Orders service with Jaina

> **What you'll have at the end:** a single-file Orders service that survives mobile retry storms, broker outages, partial failures across Payment + Shipping, and never charges a customer twice. Every step ships a passing test and a curl recipe.
>
> **What this post is not:** a marketing tour. We build it line by line. If a paragraph doesn't end with code or a curl, it gets cut.

---

## Table of contents

- [Why bother — the failure modes you're protecting against](#why-bother)
- [Chapter 1 — Skeleton: project + EF Core + first endpoint](#chapter-1)
- [Chapter 2 — The naive code, and what breaks](#chapter-2)
- [Chapter 3 — Idempotency: surviving the retry storm](#chapter-3)
- [Chapter 4 — Outbox: surviving broker outages](#chapter-4)
- [Chapter 5 — Saga: rolling back across Payment + Shipping](#chapter-5)
- [Chapter 6 — Observability: reading what your code actually did](#chapter-6)
- [Chapter 7 — Production checklist](#chapter-7)
- [Appendix A — Jaina API reference for everything we used](#appendix-a)
- [Appendix B — Common pitfalls](#appendix-b)

---

<a id="why-bother"></a>
## Why bother — the failure modes you're protecting against

You've already written this service three times. Each time, exactly one of these caught you:

| Mode | Symptom | Real-world cost |
|---|---|---|
| Mobile retries the same `POST /orders` after a 504 from your LB | Card charged twice, one order placed | Refunds + customer trust damage |
| Order writes succeed, broker `Publish` throws → `OrderPlaced` event never fires | Inventory not decremented, no shipping label, no email | Phantom orders, inventory drift |
| `Charge → Reserve → CreateShipment` succeeds for steps 1–2; step 3 fails | Card charged, inventory held, no shipment | "Where's my package?" tickets |
| Process restarted mid-flow | All of the above, with no log | The on-call playbook says "don't" |

We'll fix every one of these. The pattern names — **Idempotency**, **Outbox**, **Saga** — are not academic. Each maps directly to a failure mode above. We add them in the order they typically bite production teams.

---

<a id="chapter-1"></a>
## Chapter 1 — Skeleton: project + EF Core + first endpoint

### What we're building in this chapter

A single ASP.NET Core minimal-API service named `Orders`. EF Core in-memory provider so you don't need Postgres yet. One endpoint: `POST /orders` writes an `Order` row. That's it. We'll layer the rest on top.

### Project setup

```bash
mkdir Orders && cd Orders
dotnet new web
dotnet add package Microsoft.EntityFrameworkCore.InMemory
dotnet add package Jaina.AspNetCore
dotnet add package Jaina.HealthChecks
```

Create `Order.cs`:

```csharp
namespace Orders;

public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset PlacedAt { get; init; } = DateTimeOffset.UtcNow;
}
```

Create `OrdersDb.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Orders;

public sealed class OrdersDb : DbContext
{
    public OrdersDb(DbContextOptions<OrdersDb> options) : base(options) { }
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.Sku).HasMaxLength(50).IsRequired();
            b.Property(o => o.Total).HasPrecision(12, 2);
        });
    }
}
```

`Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Orders;
using Jaina.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrdersDb>(o => o.UseInMemoryDatabase("orders"));
builder.Services.AddJainaProblemDetails();

var app = builder.Build();
app.UseJainaPipeline();

app.MapPost("/orders", async (PlaceOrderRequest req, OrdersDb db) =>
{
    var order = new Order { Sku = req.Sku, Quantity = req.Quantity, Total = req.Quantity * req.UnitPrice };
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/orders/{id:guid}", async (Guid id, OrdersDb db) =>
    (await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id)) is { } o
        ? Results.Ok(o)
        : Results.NotFound());

app.Run();

public record PlaceOrderRequest(string Sku, int Quantity, decimal UnitPrice);
```

### Run it

```bash
dotnet run

# Place an order
curl -i -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"sku":"WIDGET","quantity":3,"unitPrice":9.99}'
# HTTP/1.1 201 Created
# Location: /orders/<guid>
# {"id":"<guid>","sku":"WIDGET","quantity":3,"total":29.97,"placedAt":"..."}

curl http://localhost:5000/orders/<guid>
# 200 with the same body
```

### What we have

- One write endpoint, one read endpoint
- ProblemDetails on errors (try `POST /orders` with no body → consistent 400)
- EF Core handling persistence

### What we don't have

Everything that matters for production. Let's break it.

---

<a id="chapter-2"></a>
## Chapter 2 — The naive code, and what breaks

### Failure scenario A — the mobile retry storm

The mobile app times out at 8 seconds. The server takes 9 seconds (TLS handshake + cold start + DB connection). Mobile retries automatically.

Reproduce with a busy-loop:

```bash
KEY=$(uuidgen)
for i in 1 2 3; do
  curl -s -X POST http://localhost:5000/orders \
       -H "Content-Type: application/json" \
       -d '{"sku":"DEMO","quantity":1,"unitPrice":1.00}' &
done
wait
```

Three orders. Three rows in the DB. Three `Total = 1.00` charges (you can imagine the payment service). The server has no way to tell that the second and third calls are retries of the first.

### Failure scenario B — the broker outage

Suppose we wanted to also send `OrderPlaced` to a message broker so other services react:

```csharp
// "Naive" two-step
db.Orders.Add(order);
await db.SaveChangesAsync();           // 1) commit row
await broker.PublishAsync(new OrderPlaced(order.Id));   // 2) publish event
```

If the broker is down for 12 seconds and step 2 throws, your DB has the row but no service downstream knows. Inventory, shipping, notifications — all blind.

This is the **dual-write problem**. The same code that "works in dev" silently fails 0.1% of the time in prod. Black Friday turns 0.1% into thousands of phantom orders.

### Failure scenario C — partial failure across services

Suppose we extended the endpoint to call three downstream services synchronously:

```csharp
db.Orders.Add(order);
await db.SaveChangesAsync();
await _inventory.ReserveAsync(order.Sku, order.Quantity);   // 1
await _payment.ChargeAsync(order.Total);                    // 2
await _shipping.CreateShipmentAsync(order.Id);              // 3 — throws 500
```

Inventory: held. Payment: charged. Shipping: error. The customer sees a 500 and tries again. Now you have 2 reservations + 2 charges + still no shipment.

We're going to fix all three in chapters 3, 4, 5 — in that order, because that's the order they tend to bite real teams.

---

<a id="chapter-3"></a>
## Chapter 3 — Idempotency: surviving the retry storm

### What "idempotency" actually means here

> A request is idempotent if making it once and making it many times produces the same observable result.

`GET` and `DELETE` are idempotent by definition. `POST` is not — that's our problem. The fix: the client supplies a stable `Idempotency-Key` header, the server caches the response against the key for some TTL, replays the cached response on duplicates.

The contract:

- The client picks the key. Convention: `<user-id>:<cart-hash>:<attempt-number>` or any stable derivation.
- The server must cache `2xx` responses verbatim (status + headers + body).
- The server must NOT cache `5xx` — those are server errors and the client SHOULD retry.
- The server SHOULD include `Idempotent-Replay: true` on cache hits so observability tooling can measure replay rates.

### Adding it to our service

Two packages:

```bash
dotnet add package Jaina.Idempotency
dotnet add package Jaina.Idempotency.InMemory
dotnet add package Jaina.Idempotency.AspNetCore
```

Three lines of wiring:

```csharp
builder.Services.AddJainaInMemoryIdempotency();   // dev/test
// or services.AddJainaRedisIdempotency() in prod, with IConnectionMultiplexer registered

app.UseJainaIdempotency();   // place AFTER auth, BEFORE endpoints
```

That's the entire change.

### How the middleware works (so you trust it)

Source: [`IdempotencyMiddleware.cs`](../../src/idempotency/Jaina.Idempotency.AspNetCore/IdempotencyMiddleware.cs).

```
incoming request
  ├── Method in {POST, PUT, PATCH, DELETE} ? else passthrough
  ├── Idempotency-Key header present and non-blank ? else passthrough
  ├── store.GetAsync(key) → existing entry?
  │     YES → write status + content-type + body from entry; add Idempotent-Replay: true
  │     NO  → swap Response.Body for a MemoryStream, await next(),
  │           if status in [200..300) → store.SetAsync(key, captured), copy back to original
```

Key points:

- The middleware doesn't validate that the body matches the cached request. A malicious / buggy client sending different bodies with the same key gets the cached response. (See the trade-offs section for how to harden.)
- The middleware refuses to cache failures. This is what makes it composable with retry policies.
- The `Idempotent-Replay: true` header is observable — your gateway can count it.

### Reproducing the retry storm — and watching it fail to leak

```bash
KEY=$(uuidgen)

# Burst three concurrent retries with the same key
for i in 1 2 3; do
  curl -s -i -X POST http://localhost:5000/orders \
       -H "Idempotency-Key: $KEY" \
       -H "Content-Type: application/json" \
       -d '{"sku":"DEMO","quantity":1,"unitPrice":1.00}' &
done
wait
```

Result on every response:

```
HTTP/1.1 201 Created
Idempotent-Replay: true
{"id":"<same-guid-each-time>","sku":"DEMO",...}
```

The body is bytes-identical across all three responses. The DB has **one** row. The "payment service" (when we add one) is called **once**.

### Test

```csharp
using Jaina.Idempotency.AspNetCore;
using Microsoft.AspNetCore.Http;

[Fact]
public async Task SecondCall_WithSameKey_ReplaysCachedResponse()
{
    var executions = 0;
    var middleware = new IdempotencyMiddleware(
        next: async ctx =>
        {
            executions++;
            ctx.Response.StatusCode = 201;
            await ctx.Response.WriteAsync("{\"id\":42}");
        },
        store: new InMemoryIdempotencyStore(new MemoryCache(new MemoryCacheOptions())),
        opts: Options.Create(new IdempotencyOptions()));

    var first = MakeContext("POST", "abc");
    await middleware.InvokeAsync(first);

    var second = MakeContext("POST", "abc");
    await middleware.InvokeAsync(second);

    Assert.Equal(1, executions);   // handler ran once
    Assert.Equal("true", second.Response.Headers["Idempotent-Replay"].ToString());
}
```

The unit test suite has 5 of these (happy path, 5xx not cached, GET ignored, etc.) — see [`IdempotencyMiddlewareTests.cs`](../../tests/unit/Jaina.Idempotency.UnitTests/IdempotencyMiddlewareTests.cs).

### What's still broken

Idempotency stops the **client side** from causing damage. It does nothing about:
- The broker being unreachable when we publish `OrderPlaced` after the DB write
- Partial failure across multi-service flows

That's chapter 4.

### Trade-offs — read this before shipping

1. **In-memory store does not work across multiple app instances.** Use Redis (`AddJainaRedisIdempotency`) when you scale beyond one pod, or every replica gives you its own dedupe and you're back to square one.
2. **TTL choice is real.** Default 24h is fine for "user retried within minutes". A user who comes back tomorrow with a stale browser tab will hit a cache miss; that's usually correct behaviour but not always.
3. **The middleware doesn't validate body matches.** If you care, hash the body, store it alongside the response, return 422 on mismatch. The library will gain this; today it's on you.
4. **Replays don't re-fire side effects.** If your `POST /orders` sends an SMS, the second call won't send a second SMS — that's the whole point. But if you actually want a second SMS for whatever reason, the SMS side effect should not be inside the cached HTTP handler. Move it to a downstream consumer.

---

<a id="chapter-4"></a>
## Chapter 4 — Outbox: surviving broker outages

### The problem in one sentence

You cannot atomically commit a database transaction *and* publish to a message broker. Step 1 succeeds, step 2 throws, your data and your broker disagree.

### The fix in one sentence

Inside the same database transaction as your domain write, insert a row into an `outbox_messages` table. A background relay polls the table, publishes each message to the broker, marks it processed.

### Adding it to our service

```bash
dotnet add package Jaina.Messaging.Outbox
dotnet add package Jaina.Messaging.Outbox.EfCore
```

Schema: tell EF Core to track the outbox entity inside the same DbContext as our orders.

```csharp
// OrdersDb.cs
using Jaina.Messaging.Outbox.EfCore;

protected override void OnModelCreating(ModelBuilder mb)
{
    mb.Entity<Order>(b => { /* ... */ });
    mb.ApplyJainaOutbox();     // adds Jaina_OutboxMessages table mapping
}
```

Wire DI:

```csharp
builder.Services.AddDbContextFactory<OrdersDb>(o => o.UseInMemoryDatabase("orders"));
builder.Services.AddDbContext<OrdersDb>(o => o.UseInMemoryDatabase("orders"));
builder.Services.AddJainaEfCoreOutbox<OrdersDb>();
builder.Services.AddSingleton<IOutboxDispatcher, ConsoleOutboxDispatcher>();
builder.Services.AddJainaOutboxRelay(o =>
{
    o.PollingInterval = TimeSpan.FromMilliseconds(500);
    o.BatchSize = 25;
});
```

A trivial dispatcher for the demo (production swaps for RabbitMQ / Service Bus / Kafka):

```csharp
public sealed class ConsoleOutboxDispatcher(ILogger<ConsoleOutboxDispatcher> log) : IOutboxDispatcher
{
    public Task DispatchAsync(OutboxMessage m, CancellationToken ct)
    {
        log.LogInformation("[outbox] {Type} -> {Dest}: {Payload}",
            m.PayloadType, m.Destination, m.Payload);
        return Task.CompletedTask;
    }
}
```

### Updating the endpoint

```csharp
public record OrderPlaced(Guid OrderId, string Sku, int Quantity, decimal Total);

app.MapPost("/orders", async (PlaceOrderRequest req, OrdersDb db, IOutbox outbox) =>
{
    var order = new Order { Sku = req.Sku, Quantity = req.Quantity, Total = req.Quantity * req.UnitPrice };
    db.Orders.Add(order);

    // Same DbContext, same transaction. EnqueueAsync just adds the row to the change tracker.
    await outbox.EnqueueAsync(
        new OrderPlaced(order.Id, order.Sku, order.Quantity, order.Total),
        destination: "orders.events",
        headers: new Dictionary<string, string> { ["correlation-id"] = Activity.Current?.Id ?? "n/a" });

    await db.SaveChangesAsync();   // ONE commit. Order row + outbox row, atomic.
    return Results.Created($"/orders/{order.Id}", order);
});
```

The endpoint is **two lines longer** than chapter 3. That's the whole change.

### How the relay loop actually works

The relay is a `BackgroundService`. Source: [`OutboxRelay.cs`](../../src/messaging/Jaina.Messaging.Outbox/OutboxRelay.cs).

```
every PollingInterval ticks:
  ├── store.ClaimBatchAsync(BatchSize) → list of messages where ProcessedAt IS NULL AND ScheduledFor <= utcNow
  ├── for each message:
  │     try: dispatcher.DispatchAsync(message)
  │          → store.MarkProcessedAsync(message.Id)   // ProcessedAt = utcNow, LastError = null
  │     catch ex:
  │          → store.MarkFailedAsync(message.Id, ex.Message, nextAttempt = utcNow + backoff(attempts+1))
  │            attempts += 1
  └── sleep PollingInterval
```

Backoff is exponential, capped at `MaxBackoff` (default 5 min): `delay = initial * 2^(n-1)`, jitter optional. After `MaxAttempts` (default 10), the message stays in the table with `LastError` set; today you scrape for it manually, parking-into-deadletter is on the roadmap.

### Reproducing the broker outage — and watching it not lose anything

Sabotage the dispatcher temporarily:

```csharp
public sealed class FlakyDispatcher(ILogger<FlakyDispatcher> log) : IOutboxDispatcher
{
    private int _calls;
    public Task DispatchAsync(OutboxMessage m, CancellationToken ct)
    {
        var n = Interlocked.Increment(ref _calls);
        if (n < 3) throw new InvalidOperationException("simulated broker outage");
        log.LogInformation("[outbox] dispatch succeeded on attempt {Attempt}", n);
        return Task.CompletedTask;
    }
}
```

Place an order:

```bash
curl -X POST http://localhost:5000/orders -H "Content-Type: application/json" -d '{"sku":"X","quantity":1,"unitPrice":1}'
```

Watch the logs:

```
[09:00:00] Outbox dispatch failed for message <id> (attempt 1); retrying at 2026-05-05T09:00:02
[09:00:02] Outbox dispatch failed for message <id> (attempt 2); retrying at 2026-05-05T09:00:06
[09:00:06] [outbox] dispatch succeeded on attempt 3
```

The 201 response went out at `09:00:00`. The downstream event landed at `09:00:06`. The **caller didn't wait** for the dispatch — that's the point. The order is durable; the event is at-least-once.

### What about consumer-side dedupe?

Outbox guarantees at-least-once. If the relay dispatches successfully but crashes before marking processed, the next iteration will dispatch the same message again. The consumer must dedupe. That's the **Inbox pattern** — symmetric to outbox, on the consumer side. We won't implement it here; it's covered separately and ships in `Jaina.Messaging.Inbox`. Add it on the consumer:

```csharp
var firstSeen = await inboxStore.TryConsumeAsync(consumer: "orders-svc", messageId: m.Id.ToString(), ttl: TimeSpan.FromDays(7));
if (!firstSeen) return;   // duplicate, ack-and-skip
// process...
```

### Inspect the outbox table

Add a debug endpoint:

```csharp
app.MapGet("/_outbox", async (OrdersDb db) =>
    Results.Ok(await db.Set<OutboxMessage>()
        .AsNoTracking()
        .OrderByDescending(m => m.CreatedAt)
        .Take(50)
        .Select(m => new { m.Id, m.PayloadType, m.Destination, m.Attempts, m.ProcessedAt, m.LastError })
        .ToListAsync()));
```

Hit `GET /_outbox` after a flaky run and you'll see the failed attempts captured in the row. Production: drop this endpoint or guard it behind admin auth.

### Test

The full Outbox test suite ([`InMemoryOutboxTests.cs`](../../tests/unit/Jaina.Messaging.Outbox.UnitTests/InMemoryOutboxTests.cs)) covers:

- enqueue then claim returns the message
- claim is exclusive (a second claim does not return the same message)
- ScheduledFor in the future is not claimed
- relay dispatches successfully and marks processed
- relay catches exceptions and reschedules

Plus the EF Core integration test ([`EfOutboxIntegrationTests.cs`](../../tests/integration/Jaina.Messaging.Outbox.EfCore.IntegrationTests/EfOutboxIntegrationTests.cs)) runs against a real Postgres container.

### What's still broken

You can lose money to **partial failure across services**. If `POST /orders` indirectly causes `Charge → Reserve → Ship` to run via three downstream services, and only the first two succeed, you're holding a charge with no shipment. The customer gets nothing. That's chapter 5.

---

<a id="chapter-5"></a>
## Chapter 5 — Saga: rolling back across Payment + Shipping

### The shape

`POST /orders` writes the row + emits `OrderPlaced` (chapter 4). A **saga** consumes `OrderPlaced` and runs:

1. `Reserve` inventory
2. `Charge` payment
3. `CreateShipment`

If step 3 fails, the saga **compensates** in reverse: refund the charge, release the reservation. We're using orchestration mode — one saga drives the steps.

### Adding it

```bash
dotnet add package Jaina.Messaging.Saga
dotnet add package Jaina.Messaging.Saga.InMemory     # dev/test
# or Jaina.Messaging.Saga.EfCore for prod (durable state)
```

State:

```csharp
public sealed class OrderSagaState : SagaState
{
    public Guid OrderId { get; init; }
    public string Sku { get; init; } = "";
    public int Quantity { get; init; }
    public decimal Amount { get; init; }
    public string? PaymentChargeId { get; set; }
    public string? ShipmentTrackingId { get; set; }
}
```

Steps — each one defines the forward action and the compensation:

```csharp
public sealed class ReserveInventoryStep(IInventoryService inv) : ISagaStep<OrderSagaState>
{
    public string Name => "ReserveInventory";

    public async Task ExecuteAsync(OrderSagaState s, CancellationToken ct) =>
        await inv.ReserveAsync(s.Sku, s.Quantity, ct);

    public async Task CompensateAsync(OrderSagaState s, CancellationToken ct) =>
        await inv.ReleaseAsync(s.Sku, s.Quantity, ct);
}

public sealed class ChargePaymentStep(IPaymentService pay) : ISagaStep<OrderSagaState>
{
    public string Name => "ChargePayment";

    public async Task ExecuteAsync(OrderSagaState s, CancellationToken ct) =>
        s.PaymentChargeId = await pay.ChargeAsync(s.OrderId, s.Amount, ct);

    public async Task CompensateAsync(OrderSagaState s, CancellationToken ct)
    {
        if (s.PaymentChargeId is { } id)
            await pay.RefundAsync(id, ct);
    }
}

public sealed class CreateShipmentStep(IShippingService ship) : ISagaStep<OrderSagaState>
{
    public string Name => "CreateShipment";

    public async Task ExecuteAsync(OrderSagaState s, CancellationToken ct) =>
        s.ShipmentTrackingId = await ship.CreateAsync(s.OrderId, s.Sku, s.Quantity, ct);

    public async Task CompensateAsync(OrderSagaState s, CancellationToken ct)
    {
        if (s.ShipmentTrackingId is { } id)
            await ship.CancelAsync(id, ct);
    }
}

public sealed class OrderSaga(
    ReserveInventoryStep reserve, ChargePaymentStep charge, CreateShipmentStep ship) : Saga<OrderSagaState>
{
    public override IReadOnlyList<ISagaStep<OrderSagaState>> Steps =>
        new ISagaStep<OrderSagaState>[] { reserve, charge, ship };
}
```

Wire DI:

```csharp
builder.Services.AddJainaSaga<OrderSaga, OrderSagaState>();
builder.Services.AddJainaInMemorySagaRepository<OrderSagaState>();
// register the step concrete types as services
builder.Services.AddScoped<ReserveInventoryStep>();
builder.Services.AddScoped<ChargePaymentStep>();
builder.Services.AddScoped<CreateShipmentStep>();
```

Run from the consumer (the thing that handles `OrderPlaced` from outbox):

```csharp
public sealed class OrderPlacedHandler(ISagaRunner<OrderSaga, OrderSagaState> runner)
{
    public async Task HandleAsync(OrderPlaced msg, CancellationToken ct)
    {
        var state = new OrderSagaState
        {
            CorrelationId = msg.OrderId,
            OrderId = msg.OrderId,
            Sku = msg.Sku,
            Quantity = msg.Quantity,
            Amount = msg.Total,
        };

        try { await runner.RunAsync(state, ct); }
        catch (SagaFailedException ex)
        {
            // ex.State carries which steps ran, which compensations succeeded, what failed
            // Decide: alert ops? push to dead-letter? notify customer of refund?
        }
    }
}
```

### How the runner works

Source: [`SagaRunner.cs`](../../src/messaging/Jaina.Messaging.Saga/SagaRunner.cs).

```
RunAsync(state):
  save state
  for each step in Steps:
    if step.Name in state.CompletedSteps: skip (resume support)
    try execute → state.CompletedSteps.Add(step.Name); save
    catch ex → state.FailedAt = step.Name; LastError = ex.Message; break
  if no failure: state.IsCompleted = true; save; return
  else: compensate

compensate:
  for each name in state.CompletedSteps reversed:
    try step.CompensateAsync → state.CompensatedSteps.Add(name)
    catch: log + continue (best-effort)
    finally: save
  state.IsCompensated = true; save
  throw SagaFailedException(state, originalException)
```

Key properties:

- **State persists between every step**. If the process crashes mid-run, the next call to `RunAsync(state)` skips the steps already in `CompletedSteps` and picks up where it left off.
- **Compensation is best-effort**. If a compensation itself throws, we log and continue with remaining compensations. The state ends with `IsCompensated = true` but the operator must inspect for partial inconsistency.
- **Order matters**. Compensations run in reverse of the completion order, not the step list order. If steps 1 and 2 ran but step 3 didn't, compensations run 2 then 1.

### Reproducing the partial failure

Throw on shipping:

```csharp
public sealed class FlakyShipping : IShippingService
{
    public Task<string> CreateAsync(Guid orderId, string sku, int qty, CancellationToken ct) =>
        throw new InvalidOperationException("shipping carrier 503");
    public Task CancelAsync(string trackingId, CancellationToken ct) => Task.CompletedTask;
}
```

Run the saga:

```
[saga abc-123] step ReserveInventory  → success
[saga abc-123] step ChargePayment     → success (PaymentChargeId=ch_42)
[saga abc-123] step CreateShipment    → FAILED: shipping carrier 503
[saga abc-123] compensating ChargePayment    → RefundAsync(ch_42) success
[saga abc-123] compensating ReserveInventory → ReleaseAsync(...) success
[saga abc-123] IsCompensated=true
SagaFailedException: Saga abc-123 failed at step 'CreateShipment': shipping carrier 503
```

Customer is refunded. Inventory is released. The saga raises `SagaFailedException` carrying the full state — the consumer decides whether to alert, retry the message later, or notify the customer.

### Crash recovery

Stop the process between step 2 and step 3. State is durable in the saga repository (with the EF Core or Redis provider — InMemory loses everything). On restart, load the state by `CorrelationId` and call `runner.RunAsync(state)` again. The runner sees `state.CompletedSteps == ["ReserveInventory", "ChargePayment"]` and skips them. Only step 3 runs.

```csharp
var state = await repository.LoadAsync(correlationId, ct);
if (state is not null)
    await runner.RunAsync(state, ct);
```

This is the "resume" tested in [`OrderSagaTests.cs:RunAsync_ResumesFromPartialState_SkipsAlreadyCompletedSteps`](../../tests/unit/Jaina.Messaging.Saga.UnitTests/OrderSagaTests.cs).

### Compensation idempotency — the part teams get wrong

Compensations may run more than once. If the saga crashes mid-compensation walk, the next attempt runs all compensations again. Refund APIs accept idempotency keys; use the saga's `CorrelationId` as part of the key:

```csharp
public async Task RefundAsync(string chargeId, CancellationToken ct)
{
    await stripe.PostAsync($"/refunds", new { charge = chargeId, idempotency_key = $"saga-refund:{state.CorrelationId}:{chargeId}" }, ct);
}
```

The third-party APIs you compose almost all support this. Use it.

### Trade-offs

- **Orchestration vs choreography.** This implementation is orchestration — one runner drives the steps. If your services react to events emitted by other services without a coordinator, you want choreography. That's a separate post; not yet shipped.
- **State is the API contract.** Once you ship a saga, the state shape is durable in the DB. Schema migrations need a versioning strategy.
- **Don't put DB updates and network calls in the same step.** If the network call succeeds and the DB write fails, you can't easily compensate the network. Split into two steps, persist between, accept the at-least-once.

---

<a id="chapter-6"></a>
## Chapter 6 — Observability: reading what your code actually did

We've added three patterns. Each one fires its own spans through `JainaActivitySource`. Wire OTEL once and you see everything.

### Setup

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
```

```csharp
using Jaina.Observability.Telemetry;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource(JainaActivitySource.Name)            // every Jaina span
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(m => m
        .AddMeter(JainaMeter.Name))                      // every Jaina counter / histogram
    .UseOtlpExporter();                                  // ships to Aspire dashboard / Tempo / Jaeger / etc.
```

That's it.

### What you'll see in production

A single order from POST through to shipment confirmation:

```
POST /orders                          api.orders.place              280ms
  jaina.tenant.id      = "acme"
  jaina.user.id        = "user_42"
  jaina.correlation.id = "abc-123"
  ├─ db.savechanges                                                   45ms
  ├─ jaina.outbox.enqueue                                              2ms
  │  └─ jaina.message.id = "m-7f"
  └─ (returns 201)

  ↓ relay tick ~500ms later

jaina.outbox.relay.tick               jaina.outbox.relay              50ms
  └─ jaina.outbox.dispatch            jaina.outbox.dispatch           45ms
     ├─ jaina.message.id     = "m-7f"
     ├─ jaina.outbox.attempt = 1
     ├─ http.client.duration                                          40ms
     │  └─ POST orders.events.svc -> 200 OK

  ↓ saga handler picks up the message

jaina.saga.run                        jaina.saga.run                  410ms
  jaina.saga.correlation_id = "abc-123"
  ├─ jaina.saga.step                  ReserveInventory                 60ms
  ├─ jaina.saga.repo.save                                               2ms
  ├─ jaina.saga.step                  ChargePayment                   180ms
  │  └─ http.client.duration  POST stripe.com/charges                 175ms
  ├─ jaina.saga.repo.save                                               2ms
  ├─ jaina.saga.step                  CreateShipment                  120ms
  │  └─ http.client.duration  POST shipping.svc                       115ms
  └─ jaina.saga.repo.save                                               2ms
```

You can read the whole flow top-down. If the `payment.charge` span is the one taking 5 seconds, you know exactly where to look.

### Tag conventions for slicing dashboards

Source: [`TagConventions.cs`](../../src/observability/Jaina.Observability/Telemetry/TagConventions.cs).

```
jaina.tenant.id          (low cardinality — safe for dashboards)
jaina.correlation.id     (high cardinality — safe for traces, NOT for metrics)
jaina.user.id            (medium cardinality — careful with metrics)
jaina.cache.hit          (boolean tag, perfect for histograms)
jaina.outbox.attempt     (small int, gauge slice key)
jaina.idempotency.replay (boolean — count replays per tenant)
```

Sample queries you can paste into Grafana:

```promql
# Per-tenant latency p99
histogram_quantile(0.99, sum(rate(http_server_duration_seconds_bucket[5m])) by (le, jaina_tenant_id))

# Outbox lag — messages with Attempts > 1 over time
rate(jaina_outbox_failed_total[5m]) by (jaina_tenant_id)

# Idempotent-replay rate (find clients retrying too much)
sum(rate(http_server_responses_total{idempotent_replay="true"}[5m])) by (jaina_tenant_id)
```

### Custom spans in your code

Anywhere your code does meaningful work:

```csharp
using var span = JainaActivitySource.StartSpan("orders", "validate-cart");
span?.SetTag(TagConventions.TenantId, tenantId);
// ... do the work
```

The `using` block automatically ends the span when the scope exits. If no exporter is subscribed, `StartSpan` returns null and the cost is one virtual call.

### Reading a real failure

Customer reports order #4827 was charged twice. Open the trace by `correlation-id = "..."`:

- One `POST /orders` → 201 Created
- One `outbox.dispatch` → success
- Two `saga.run` spans (different `saga.correlation_id`!)

That tells you: the **consumer** (saga) is processing the message twice without dedupe. You need an Inbox between the broker and the saga handler. Add `Jaina.Messaging.Inbox` and `inboxStore.TryConsumeAsync(...)` in front of `runner.RunAsync(...)`.

You couldn't have figured that out from logs alone.

---

<a id="chapter-7"></a>
## Chapter 7 — Production checklist

You've added Idempotency + Outbox + Saga + Observability to a service that started as 30 lines. Before flipping prod traffic:

### Storage

- [ ] **Outbox**: switch from `InMemory` to `EfCore`. Confirm your DbContext shares the connection with the rest of the service (so writes are atomic).
- [ ] **Idempotency**: switch to Redis (`AddJainaRedisIdempotency`) so all replicas share the dedup state.
- [ ] **Inbox** (consumer side): same — Redis or EfCore.
- [ ] **Saga state**: EF Core or Redis, never InMemory.

### Configuration

- [ ] `OutboxOptions.PollingInterval` — 500ms in low-traffic, 100ms with `LISTEN/NOTIFY` (Postgres) when you need sub-second.
- [ ] `OutboxOptions.MaxAttempts` — 10 by default. Tune to your downstream's typical recovery window.
- [ ] `IdempotencyOptions.DefaultTtl` — 24h is fine for retry-storms; longer for "user comes back tomorrow" scenarios.
- [ ] `JainaResiliencePipelines.ExternalHttp` for downstream clients (chapter 4 dispatcher) — already shipped, just `services.AddJainaResilience()` and use the named pipeline.

### Observability

- [ ] `AddSource(JainaActivitySource.Name)` and `AddMeter(JainaMeter.Name)` in OTEL setup.
- [ ] Export to OTLP (Aspire dashboard, Tempo, Jaeger).
- [ ] Alert on `jaina.outbox.pending` growing without bound (broker outage signal).
- [ ] Alert on `jaina.saga.compensation_failed` > 0 (manual cleanup needed).
- [ ] Alert on idempotent-replay rate spike (client retrying too aggressively).

### Health checks

- [ ] `services.AddHealthChecks().AddCheck("self", _ => HealthCheckResult.Healthy(), tags: ["live"])` — process-up.
- [ ] `.AddCheck("outbox-lag", new OutboxLagHealthCheck(...), tags: ["ready"])` — fail readiness when the relay falls > N minutes behind.
- [ ] `.AddCheck("redis", ..., tags: ["ready"])` if you use it.
- [ ] `app.MapJainaHealthChecks()` exposes `/health/live` + `/health/ready`.

### Data hygiene

- [ ] Schedule a daily job to delete `OutboxMessages WHERE ProcessedAt < utcNow - 7 days`. Outbox tables grow.
- [ ] Same for completed sagas, idempotency entries beyond TTL.

### Error handling

- [ ] Compensations include an idempotency key derived from `state.CorrelationId`.
- [ ] Failed sagas are surfaced as alerts, not silent log lines.
- [ ] `LastError` field is greppable — keep it short and structured.

If you can tick all of these, you're ready for Black Friday.

---

<a id="appendix-a"></a>
## Appendix A — Jaina API reference for everything we used

### `Jaina.AspNetCore`

| Method | Purpose |
|---|---|
| `services.AddJainaProblemDetails()` | Standard ProblemDetails with exception-to-status mapping |
| `app.UseJainaPipeline()` | Composes `UseExceptionHandler` + `UseStatusCodePages` |
| `endpoint.WithJainaResultFilter()` | Maps `Result<T>` returns to `IResult` automatically |

### `Jaina.Idempotency`

| Method | Purpose |
|---|---|
| `services.AddJainaInMemoryIdempotency()` | Register in-memory store + options |
| `services.AddJainaRedisIdempotency()` | Register Redis-backed store |
| `app.UseJainaIdempotency()` | HTTP middleware that reads `Idempotency-Key` |

`IdempotencyOptions` properties: `HeaderName` (default `Idempotency-Key`), `DefaultTtl` (default 24h), `CacheableMethods` (default POST/PUT/PATCH/DELETE).

### `Jaina.Messaging.Outbox`

| Method | Purpose |
|---|---|
| `IOutbox.EnqueueAsync<T>(message, destination, headers, scheduledFor)` | Producer-side: adds to current DbContext (with EfCore provider) |
| `IOutboxStore.AddAsync / ClaimBatchAsync / MarkProcessedAsync / MarkFailedAsync` | Relay-side primitives |
| `IOutboxDispatcher.DispatchAsync(OutboxMessage)` | Caller-supplied broker publish |
| `services.AddJainaOutboxRelay(o => ...)` | Registers `OutboxRelay` as `BackgroundService` |
| `services.AddJainaEfCoreOutbox<TDb>()` | Wires EF-backed `IOutbox` + `IOutboxStore` |
| `modelBuilder.ApplyJainaOutbox()` | Maps `OutboxMessage` to `Jaina_OutboxMessages` |

`OutboxOptions`: `PollingInterval`, `BatchSize`, `MaxAttempts`, `InitialBackoff`, `MaxBackoff`.

### `Jaina.Messaging.Saga`

| Method | Purpose |
|---|---|
| `Saga<TState>` (abstract) | Domain saga inherits and supplies `Steps` |
| `ISagaStep<TState>.ExecuteAsync / CompensateAsync` | Forward + reverse |
| `ISagaRunner<TSaga, TState>.RunAsync(state)` | Executes the saga, persists between steps |
| `ISagaRepository<TState>.LoadAsync / SaveAsync` | Persistence — InMemory / EfCore / Redis |
| `services.AddJainaSaga<TSaga, TState>()` | Registers saga + runner |
| `services.AddJainaInMemorySagaRepository<TState>()` | Dev/test repo |
| `services.AddJainaEfCoreSagaRepository<TState, TDb>()` | Production repo |

`SagaState` carries `CorrelationId`, `CompletedSteps`, `CompensatedSteps`, `FailedAt`, `LastError`, `IsCompleted`, `IsCompensated`.

### `Jaina.Observability.Telemetry`

| API | Purpose |
|---|---|
| `JainaActivitySource.Name = "Jaina"` | OTEL source name to subscribe |
| `JainaActivitySource.StartSpan(module, op)` | Start a span named `jaina.<module>.<op>` |
| `JainaMeter.Name = "Jaina"` | OTEL meter name to subscribe |
| `TagConventions.*` | Constants for standard tag keys |

---

<a id="appendix-b"></a>
## Appendix B — Common pitfalls

### "I added Outbox but my events still don't fire"

You forgot `await db.SaveChangesAsync()`. `EnqueueAsync` only adds to the change tracker — the user's `SaveChanges` is what commits. If you have an "always rollback in dev" pattern, the outbox row rolls back too. Good — that's the atomicity you paid for.

### "My saga is running each step twice"

Two consumers reading the same broker message without dedup. Add an Inbox in front of the saga runner — `Jaina.Messaging.Inbox`. The Inbox.TryConsumeAsync returns false on duplicate, you ack the message and skip the saga.

### "My idempotency middleware caches everything including health checks"

It only matches `POST/PUT/PATCH/DELETE` by default. Health checks are GET. If you see otherwise, check the `CacheableMethods` option — someone configured it.

### "Outbox is fine but the compensation in my saga also charges the card again"

Compensations need to be idempotent. The saga runner may re-run compensations after a crash. Use the saga's `CorrelationId` as the idempotency key for any external API calls in compensation methods.

### "Trace shows everything green but the customer didn't get the order"

Look outside the trace. Common cause: an exception was caught and swallowed without `Activity.SetStatus(ActivityStatusCode.Error)`. Or your span sampler dropped the failed traces.

### "OutboxMessage table is 10GB"

Schedule a delete job: `DELETE FROM jaina_outbox_messages WHERE ProcessedAt < utcNow - 7 days`. Same for inbox + saga state for completed sagas.

### "Saga state migrations are killing me"

Once a saga has been deployed, the state shape is durable. Either: never break the JSON shape; or version the state type (`OrderSagaState_v2`) and migrate on read. Same problem as event sourcing.

---

## Where to next

You've built the canonical e-commerce vertical: idempotent intake → durable event → distributed transaction with rollback → traceable from end to end. The same shapes apply to anything order-shaped: payments, bookings, fulfilment, content publishing.

Companion posts when you're ready to go deeper:

- [Outbox: never lose another order on Black Friday](2026-05-04-outbox-black-friday.md) — the chapter 4 deep dive
- [Saga orchestration: Payment + Shipping rollback](2026-05-05-saga-orchestration.md) — chapter 5 deep dive with more failure modes
- [Idempotency: surviving the mobile retry storm](2026-05-04-idempotency-retry-storm.md) — chapter 3 deep dive
- [Reading OTEL traces like a novel](2026-05-05-observability-traces.md) — chapter 6 standalone
- [Multi-tenant SaaS](2026-05-05-multi-tenancy.md) — when you need per-tenant isolation
- [Migrating monolith → microservices](2026-05-05-monolith-to-microservices.md) — sequencing the rollout

Sample to clone and run:

```bash
git clone https://github.com/HoangSnowy/jaina-dotnet
cd jaina-dotnet
dotnet run --project samples/JainaShop/JainaShop.AppHost
# Open the Aspire dashboard URL printed in the console
```

`JainaShop.Orders` is the production-shaped variant of what you built here.
