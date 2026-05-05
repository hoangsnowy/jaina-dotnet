---
title: "Payment + Shipping rollback gracefully with Saga orchestration"
date: 2026-05-05
tags: [saga, orchestration, distributed-transactions, microservices]
reading_time: "~10 min"
sample: tests/unit/Jaina.Messaging.Saga.UnitTests/OrderSagaTests.cs
---

# Payment + Shipping rollback gracefully with Saga orchestration

## The Story

Friday morning. Marketing launched the new bundle deal at 9 AM. By 9:14 AM your support queue has 30 tickets all variations of the same complaint: "**You charged my card twice and shipped only one box.**" You dig in: the new flow is `Reserve inventory → Charge payment → Create shipment`. The shipment service is throwing 500s on every other call (capacity bug). The payment was charged, the inventory was reserved, but no shipment was created. The customer was charged for a phantom order.

You can't put all three in one DB transaction — they're three different services. You need the **Saga pattern**: do the steps in order, and if any fails, **walk back the completed ones in reverse with compensations**.

## Naive approach

```csharp
public async Task<Result> PlaceOrderAsync(OrderRequest req, CancellationToken ct)
{
    await _inventory.ReserveAsync(req, ct);
    await _payments.ChargeAsync(req, ct);   // ← if this throws, inventory is leaked
    await _shipping.CreateAsync(req, ct);   // ← if this throws, the customer was charged
    return Result.Ok();
}
```

What breaks:

- Shipping fails → the card is charged but no shipment exists. Refund flow is manual.
- Service crashes between any two steps → same orphan state, with even less audit trail.
- Retrying the whole method re-reserves and re-charges (no idempotency, see [the previous post](2026-05-04-idempotency-retry-storm.md)).
- "Just rollback the DB" doesn't help — the payment lives in Stripe, the shipment in FedEx, the inventory in the warehouse system. There is no shared transaction.

## Jaina solution

```csharp
public sealed class OrderSagaState : SagaState
{
    public string Sku { get; init; } = "";
    public int Quantity { get; init; }
    public string? PaymentChargeId { get; set; }
    public string? ShipmentTrackingId { get; set; }
}

public sealed class OrderSaga : Saga<OrderSagaState>
{
    public OrderSaga(InventoryService inv, PaymentService pay, ShippingService ship)
    {
        Steps = new ISagaStep<OrderSagaState>[]
        {
            new ReserveInventoryStep(inv),
            new ChargePaymentStep(pay),
            new CreateShipmentStep(ship),
        };
    }
    public override IReadOnlyList<ISagaStep<OrderSagaState>> Steps { get; }
}

public sealed class ChargePaymentStep : ISagaStep<OrderSagaState>
{
    private readonly PaymentService _pay;
    public ChargePaymentStep(PaymentService pay) => _pay = pay;
    public string Name => "ChargePayment";

    public async Task ExecuteAsync(OrderSagaState s, CancellationToken ct) =>
        s.PaymentChargeId = await _pay.ChargeAsync(s.Sku, s.Quantity, ct);

    public async Task CompensateAsync(OrderSagaState s, CancellationToken ct)
    {
        if (s.PaymentChargeId is { } id)
            await _pay.RefundAsync(id, ct);
    }
}
```

DI:

```csharp
services.AddJainaSaga<OrderSaga, OrderSagaState>();
services.AddJainaInMemorySagaRepository<OrderSagaState>();   // dev/test
// or services.AddJainaEfCoreSagaRepository<OrderSagaState, AppDb>();   // prod
```

Run:

```csharp
public async Task<Result> PlaceOrderAsync(OrderRequest req, ISagaRunner<OrderSaga, OrderSagaState> runner)
{
    var state = new OrderSagaState { Sku = req.Sku, Quantity = req.Quantity };
    try
    {
        await runner.RunAsync(state);
        return Result.Ok();
    }
    catch (SagaFailedException ex)
    {
        return Result.Fail($"Order failed at step {ex.State.FailedAt}; compensations: {string.Join(",", ex.State.CompensatedSteps)}");
    }
}
```

Source: [`SagaRunner.cs`](../../src/messaging/Jaina.Messaging.Saga/SagaRunner.cs).

## Happy path

```
[09:00:00] saga abc-123 step 1/3 ReserveInventory      success (state saved)
[09:00:00] saga abc-123 step 2/3 ChargePayment         success (state saved, PaymentChargeId=ch_42)
[09:00:01] saga abc-123 step 3/3 CreateShipment        success (state saved, ShipmentTrackingId=trk_88)
[09:00:01] saga abc-123 IsCompleted=true
```

State persisted after every step — a crash mid-way is recoverable.

## Error scenarios

### 1. Shipment service throws on the third step

The middle real-world case. After `ReserveInventory` and `ChargePayment` succeed, `CreateShipment` throws.

The runner walks the completed steps in **reverse**:

```
[09:14:32] saga def-456 step 3/3 CreateShipment        FAILED: 500 from shipping API
[09:14:32] saga def-456 compensating ChargePayment     RefundAsync(ch_99) → success
[09:14:32] saga def-456 compensating ReserveInventory  ReleaseAsync(...)  → success
[09:14:32] saga def-456 IsCompensated=true
```

Customer is refunded, inventory released. The saga throws `SagaFailedException` carrying the full state so the caller can decide whether to surface the failure to the user, retry, or alert ops.

### 2. The first step throws — nothing to compensate

```
[09:14:32] saga ghi-789 step 1/3 ReserveInventory      FAILED: out of stock
[09:14:32] saga ghi-789 IsCompensated=true (no prior steps)
```

`SagaFailedException.State.CompletedSteps` is empty; `CompensatedSteps` is empty. Clean failure, no side effects, terminal.

### 3. Resume after process crash

The relay is killed mid-saga (deploy, crash, k8s eviction). On restart, load the state by correlation id and call `RunAsync(state)` again. The runner sees `state.CompletedSteps` already contains the steps that ran and **skips them**, picking up at the first unfinished step. No duplicate `ChargeAsync` calls.

This requires the storage provider to actually persist — the in-memory provider loses everything on restart, so for production use `services.AddJainaEfCoreSagaRepository<OrderSagaState, AppDb>()` or `AddJainaRedisSagaRepository<OrderSagaState>()`.

### 4. A compensation itself fails

Pat refund API is down too. The runner logs at `Error`, **continues compensating the remaining steps**, and finishes with `IsCompensated=true` but the state shows incomplete `CompensatedSteps`.

```
[09:14:32] compensating ChargePayment  FAILED: payment API 503
[09:14:32] compensating ReserveInventory success
[09:14:32] saga IsCompensated=true   ← terminal, but check state.CompensatedSteps for partial
```

This is a **best-effort** policy: it's better to at least release the inventory than to skip everything because one compensation failed. The saga is now in an inconsistent state (charge still on the card, inventory released) and an operator must intervene. Alert on `LastError != null && IsCompensated` to find these.

### 5. Concurrent saga instances racing

Each saga has a distinct `CorrelationId`. Two concurrent orders write to two different rows; the EF Core or Redis provider isolates them naturally. There's no contention by default — until you start sharing state across sagas, in which case use the standard pessimistic / optimistic concurrency tools of your repository.

### 6. A step takes 5 minutes (long-running call)

Saga steps are awaitable; they can take as long as they need. But the host that runs the saga must stay alive for that duration. For genuinely long steps (human approval, batch jobs taking hours), consider splitting into multiple sagas connected by events — that's choreography and lands in 1.1.

## What you'd see in production

OTEL trace for a successful saga (3 steps):

```
saga.run                  jaina.saga.run               45ms
├─ saga.step.Reserve       jaina.saga.step              12ms
├─ saga.repo.save          jaina.saga.repo               2ms
├─ saga.step.Charge        jaina.saga.step              25ms (calls Stripe)
├─ saga.repo.save          jaina.saga.repo               2ms
├─ saga.step.Ship          jaina.saga.step               4ms
└─ saga.repo.save          jaina.saga.repo               2ms
```

Useful metrics:

- `jaina.saga.duration` histogram by name — see when sagas slow down
- `jaina.saga.failed` counter by `failed_at_step` — find which step is the culprit
- `jaina.saga.compensation_failed` counter — these are the on-call pages

## Trade-offs & gotchas

- **Compensations must be idempotent.** A crashed runner may re-run the same compensation. Refund APIs usually accept an idempotency key — use the saga's `CorrelationId` as part of it.
- **Don't put network calls in the same step as DB updates.** If the network call succeeds and the DB write fails, you can't easily compensate the network. Split into two steps, persist between, then live with the at-least-once semantics on the network side.
- **State is the API contract**. Once you ship a saga, the state shape is durable in the DB. Changes need migration; consider versioning the state type.
- **Orchestration vs choreography**: this implementation is orchestration (one saga drives the steps). If your services need to react to events emitted by other services without a central coordinator, you want choreography — that's a future module.

## Try it yourself

The runner is exercised in unit tests with deterministic step controllers — no broker required:

```bash
dotnet test tests/unit/Jaina.Messaging.Saga.UnitTests/Jaina.Messaging.Saga.Tests.csproj -f net8.0
```

You can also wire it end-to-end in the sample WebApi by adding the `OrderSaga` above and a `POST /api/orders/saga` endpoint that calls `runner.RunAsync(state)`.

## Further reading

- Source: [`SagaRunner.cs`](../../src/messaging/Jaina.Messaging.Saga/SagaRunner.cs), [`InMemorySagaRepository.cs`](../../src/messaging/Jaina.Messaging.Saga.InMemory/InMemorySagaRepository.cs), [`EfSagaRepository.cs`](../../src/messaging/Jaina.Messaging.Saga.EfCore/EfSagaRepository.cs), [`RedisSagaRepository.cs`](../../src/messaging/Jaina.Messaging.Saga.Redis/RedisSagaRepository.cs)
- Tests (5/5 — happy path, mid-step failure with compensation, first-step failure, resume from partial, persistence): [`OrderSagaTests.cs`](../../tests/unit/Jaina.Messaging.Saga.UnitTests/OrderSagaTests.cs)
- Companion posts: [Outbox](2026-05-04-outbox-black-friday.md) for the producer side, [Idempotency](2026-05-04-idempotency-retry-storm.md) for handler-level dedup.
