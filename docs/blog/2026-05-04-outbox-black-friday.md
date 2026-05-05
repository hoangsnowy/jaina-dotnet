---
title: "Outbox: never lose another order on Black Friday"
date: 2026-05-04
tags: [messaging, outbox, transactional, microservices]
reading_time: "~9 min"
sample: samples/JainaShop/JainaShop.AppHost/Program.cs
---

# Outbox: never lose another order on Black Friday

## The Story

11:58 PM, Black Friday-eve. Your shop has been load-tested at 50k requests/second. You ship. At 00:03 the order graph dips by 30% — but checkout latency is fine, payment service is healthy. What's wrong?

Eventually the on-call engineer notices that a RabbitMQ node was restarted by an automated cluster operation, and during the 12-second leadership election, every `OrderService.PlaceOrder()` that completed its database write also tried to publish to `orders.events` — and threw. The order is in the database. The event is gone. Inventory was never decremented. Notifications never sent. Recommendations stale for weeks.

The bug is the classic **dual-write problem**: you cannot atomically commit a database transaction *and* publish to a broker. Even if both succeed 99.9% of the time, the 0.1% will eat you on Black Friday.

The fix is the **Transactional Outbox**:

1. Inside the same DB transaction as the business write, insert a row into an `outbox_messages` table.
2. A background relay polls the outbox, publishes each message to the broker, marks it processed.
3. Crashes between step 1 and step 2? The next relay iteration picks the message up.
4. Crashes between publish and mark-processed? At-least-once delivery — combine with **Inbox** dedup on the consumer.

## Naive approach

```csharp
app.MapPost("/api/orders", async (OrderRequest req, AppDb db, IQueue<OrderPlaced> queue) =>
{
    var order = new Order(req.Sku, req.Quantity);
    db.Orders.Add(order);
    await db.SaveChangesAsync();                                  // ← step 1: DB committed

    await queue.PublishAsync(new OrderPlaced(order.Id));          // ← step 2: broker publish
    return Results.Created($"/api/orders/{order.Id}", null);
});
```

What breaks:

- Broker is unavailable for 12s → `PublishAsync` throws → handler returns 500 → DB row stays committed → event never sent.
- Process is killed between the two awaits → same outcome.
- Even with retries, you're racing against the request lifetime — the client times out.

## Jaina solution

```csharp
// Program.cs
builder.Services.AddJainaInMemoryOutbox();           // dev/test; .EfCore lands next
builder.Services.AddSingleton<IOutboxDispatcher, ConsoleOutboxDispatcher>();
builder.Services.AddJainaOutboxRelay(o =>
{
    o.PollingInterval = TimeSpan.FromMilliseconds(500);
    o.BatchSize = 25;
});

// Endpoint — single line to enqueue
app.MapPost("/api/outbox/order-placed", async (OrderRequest req, IOutbox outbox) =>
{
    await outbox.EnqueueAsync(
        new OrderPlacedEvent(Guid.NewGuid(), req.Sku, req.Quantity),
        destination: "orders.events",
        headers: new Dictionary<string, string> { ["correlation-id"] = Activity.Current?.Id ?? "n/a" });
    return Results.Accepted(value: new { message = "Event enqueued; relay will dispatch shortly" });
});
```

In production with the EF Core provider (next commit), `outbox.EnqueueAsync` writes to the same `DbContext` as your `Order` entity. They commit together or not at all. The relay handles the rest.

Source: [`OutboxRelay.cs`](../../src/messaging/Jaina.Messaging.Outbox/OutboxRelay.cs).

## Happy path

```bash
# Enqueue
$ curl -X POST http://localhost:5000/api/outbox/order-placed \
    -H "Content-Type: application/json" \
    -d '{"sku":"WIDGET-001","quantity":3}'
{"message":"Event enqueued; relay will dispatch shortly"}

# Server logs (~500ms later, after one relay tick)
[10:30:00] Outbox relay starting (poll 00:00:00.500, batch 25)
[10:30:00] [outbox] dispatch d4ad... type=OrderPlacedEvent dest=orders.events payload={"OrderId":"...","Sku":"WIDGET-001","Quantity":3}

# Snapshot endpoint shows the message processed
$ curl http://localhost:5000/api/outbox/snapshot
[{
  "id":"d4ad-...","payloadType":"OrderPlacedEvent","destination":"orders.events",
  "attempts":0,"processedAt":"2026-05-04T10:30:00.512Z","lastError":null
}]
```

## Error scenarios

### 1. Broker is down when the relay tries to publish

The dispatcher throws. The relay catches the exception, calls `MarkFailedAsync` with the error string, increments `Attempts`, and reschedules `ScheduledFor` using exponential backoff (initial 2s, doubling, capped at 5 min by default).

Logs:

```
[10:30:00] Outbox dispatch failed for message d4ad-... (attempt 1); retrying at 2026-05-04T10:30:02
[10:30:02] Outbox dispatch failed for message d4ad-... (attempt 2); retrying at 2026-05-04T10:30:06
[10:30:06] Outbox dispatch failed for message d4ad-... (attempt 3); retrying at 2026-05-04T10:30:14
…
[10:32:34] [outbox] dispatch d4ad-... (broker back, success)
```

The message is in the DB the whole time. No business state lost.

### 2. Application process crashes between DB commit and the next relay tick

The DB row in `outbox_messages` survives. When the process restarts, the relay picks it up on the first poll. At-least-once delivery; the message will be sent.

### 3. Relay crashes mid-batch (partial dispatch)

Each message is marked `processed` individually after a successful dispatch. A crash mid-batch means the unmarked messages remain claimed-but-unfinished. With the in-memory provider that's a minor leak; with EF Core, the claim is in a transaction or row-lock that releases on disconnect.

In all cases the consumer must dedup — see the **Inbox pattern** post (next milestone).

### 4. Poison message (one specific payload always throws on dispatch)

The relay keeps retrying with exponential backoff up to `MaxAttempts` (default 10). After that you have a few options:

- Park into a dead-letter table for manual inspection.
- Forward to a "DLQ topic" via a separate dispatcher.
- Alert on `Attempts >= MaxAttempts - 1` so you investigate before the message is dropped.

The current relay does *not* yet park automatically — that lands when EF Core provider does. Today, `Attempts` keeps incrementing and `LastError` keeps the latest message; observability metric on this counter is a leading indicator.

### 5. Two relay instances running (HA deployment)

The in-memory store has no protection against this — both processes hold their own dictionary. The EF Core provider uses `SELECT … FOR UPDATE SKIP LOCKED` (Postgres) or row-locking (SQL Server) so concurrent relays partition the work. Don't run multiple relays against the in-memory provider.

### 6. Scheduled message arrives early (clock skew)

`ScheduledFor` is a UTC timestamp. The relay only claims messages where `ScheduledFor <= UtcNow`. Modest clock skew (sub-second) just delays delivery slightly. Large skew (minutes) on the relay host will starve scheduled messages — fix the host, don't compensate in code.

## What you'd see in production

OTEL trace for a single order:

```
POST /api/orders    span: api.orders.place         200ms
├─ db.savechanges   span: db.savechanges            45ms
├─ outbox.enqueue   span: jaina.outbox.enqueue       2ms
└─ (returns 202)
…  500ms later …
relay tick          span: jaina.outbox.relay.tick   ~50ms
└─ outbox.dispatch  span: jaina.outbox.dispatch     ~30ms
   └─ rabbit.publish span: rabbitmq.publish          ~25ms
```

The first three steps share a trace ID via the `correlation-id` header carried in the outbox message — the relay propagates it through `Activity.Current` so the dispatch span links back.

Useful metrics:

- `jaina.outbox.pending` (gauge) — how many messages are waiting; alert if it grows without bound.
- `jaina.outbox.attempts` histogram by `dest` — find which destinations are flaky.
- `jaina.outbox.relay.tick.duration` — relay healthiness.

## Trade-offs & gotchas

- **At-least-once, not exactly-once.** Consumers must dedup. Use the Inbox pattern: store `(consumer_name, message_id)` and ack-then-skip duplicates.
- **Outbox table grows.** Add a periodic cleanup job for `processed_at < now - 7 days` (or whatever your retention).
- **Latency floor**: there's a polling delay (default 500ms in the sample, 1s by default). For sub-100ms publishing latency, use Postgres `LISTEN/NOTIFY` to wake the relay instead of polling — that lands in the EF Core provider.
- **Headers propagate; the message body is your contract.** Include schema version (`schema-version: v1`) so consumers can evolve.
- **Don't put PII in the outbox unless your DB encrypts at rest.** Outbox is a database table; treat it like one.

## Try it yourself

```bash
git clone https://github.com/HoangSnowy/jaina-dotnet
cd jaina-dotnet
dotnet run --project samples/JainaShop/JainaShop.AppHost

# Terminal 2 — enqueue 5 orders
for i in 1 2 3 4 5; do
  curl -s -X POST http://localhost:5000/api/outbox/order-placed \
    -H "Content-Type: application/json" \
    -d "{\"sku\":\"DEMO-$i\",\"quantity\":1}" | jq .
done

# Watch the relay logs in terminal 1; ~500ms-1s later you'll see five dispatch lines.

# Snapshot endpoint shows everything processed
curl -s http://localhost:5000/api/outbox/snapshot | jq .
```

To simulate failure scenario #1, change `ConsoleOutboxDispatcher.DispatchAsync` to throw on the first call. Hit the enqueue endpoint, then watch the retry/backoff cadence in logs.

## Further reading

- Source: [`OutboxRelay.cs`](../../src/messaging/Jaina.Messaging.Outbox/OutboxRelay.cs), [`InMemoryOutboxStore.cs`](../../src/messaging/Jaina.Messaging.Outbox.InMemory/InMemoryOutboxStore.cs)
- Tests (5 cases including dispatch-fails-then-reschedules): [`InMemoryOutboxTests.cs`](../../tests/Jaina.Messaging.Outbox.Tests/InMemoryOutboxTests.cs)
- Companion post: [Idempotency: surviving the mobile retry storm](2026-05-04-idempotency-retry-storm.md) — pairs with outbox to give you producer-side reliability + consumer-side dedup.
- Coming soon: the EF Core provider (transactional enqueue inside `SaveChanges`), Inbox pattern (consumer dedup), and Saga (cross-service compensations).
