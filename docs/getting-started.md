# Getting started

Stand up an Orders service with idempotency + outbox + observability in 10 minutes.

## Prerequisites

- .NET 8 SDK or later
- Docker (optional — only needed for the EF Core / Redis / RabbitMQ integration tests)

## 1. New project

```bash
dotnet new web -o Orders
cd Orders
```

## 2. Install the modules you need

```bash
dotnet add package Jaina.AspNetCore                  # ProblemDetails + Result<T> filter + UseJainaPipeline
dotnet add package Jaina.Idempotency.AspNetCore      # HTTP Idempotency-Key middleware
dotnet add package Jaina.Idempotency.InMemory        # dev/test store; swap for .Redis in prod
dotnet add package Jaina.Messaging.Outbox.EfCore     # transactional outbox over your DbContext
dotnet add package Jaina.HealthChecks                # /health/live + /health/ready
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

## 3. Wire it up

```csharp
using Jaina.AspNetCore;
using Jaina.HealthChecks;
using Jaina.Idempotency.AspNetCore;
using Jaina.Idempotency.InMemory;
using Jaina.Messaging.Outbox;
using Jaina.Messaging.Outbox.EfCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJainaProblemDetails();
builder.Services.AddJainaInMemoryIdempotency();
builder.Services.AddDbContextFactory<OrdersDb>(o => o.UseInMemoryDatabase("orders"));
builder.Services.AddDbContext<OrdersDb>(o => o.UseInMemoryDatabase("orders"));
builder.Services.AddJainaEfCoreOutbox<OrdersDb>();
builder.Services.AddSingleton<IOutboxDispatcher, ConsoleOutboxDispatcher>();
builder.Services.AddJainaOutboxRelay();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: new[] { JainaHealthCheckTags.Live });

var app = builder.Build();

app.UseJainaPipeline();
app.UseJainaIdempotency();
app.MapJainaHealthChecks();

app.MapPost("/orders", async (PlaceOrderRequest req, OrdersDb db, IOutbox outbox) =>
{
    var order = new Order { Sku = req.Sku, Quantity = req.Quantity };
    db.Orders.Add(order);
    await outbox.EnqueueAsync(new OrderPlaced(order.Id, order.Sku, order.Quantity), destination: "orders.events");
    await db.SaveChangesAsync();
    return Results.Created($"/orders/{order.Id}", order);
});

app.Run();
```

## 4. Run

```bash
dotnet run
```

## 5. Try it

### Place an order

```bash
$ curl -i -X POST http://localhost:5000/orders \
       -H "Idempotency-Key: customer-42-cart-7" \
       -H "Content-Type: application/json" \
       -d '{"sku":"WIDGET","quantity":3}'
```

**Output:**

```http
HTTP/1.1 201 Created
Location: /orders/0e76...
Content-Type: application/json

{ "id":"0e76...", "sku":"WIDGET", "quantity":3 }
```

Logs (relay tick ~500ms later):

```
[outbox] dispatch 7f3a-... type=OrderPlaced dest=orders.events
```

### Replay the same request — no double-write

```bash
$ curl -i -X POST http://localhost:5000/orders \
       -H "Idempotency-Key: customer-42-cart-7" \
       -H "Content-Type: application/json" \
       -d '{"sku":"WIDGET","quantity":3}'
```

**Output:**

```http
HTTP/1.1 201 Created
Idempotent-Replay: true
Content-Type: application/json

{ "id":"0e76...", "sku":"WIDGET", "quantity":3 }
```

Same `id`. Same body. The `Idempotent-Replay: true` header tells observability tooling this was a replay.

### Health probes

```bash
$ curl http://localhost:5000/health/live
Healthy

$ curl http://localhost:5000/health/ready
Healthy
```

## What just happened

- `UseJainaPipeline()` wired exception handling + ProblemDetails for consistent error shapes.
- `UseJainaIdempotency()` cached the first 201 response keyed by your header. The second call replayed it instead of executing the handler.
- `IOutbox.EnqueueAsync` added an `OutboxMessage` row to the same `DbContext` as the order. `SaveChangesAsync` committed both atomically — no dual-write problem.
- The relay loop (`AddJainaOutboxRelay`) polled the table and dispatched the message asynchronously. Your handler returned 201 in milliseconds; the broker dispatch ran out-of-band.

## Where next

- [Cookbook](blog/README.md) — runnable recipes per pattern, each with happy path + 4–6 error scenarios.
- [📘 Ebook](blog/2026-05-05-orders-service-from-scratch.md) — the same Orders service, end-to-end, with every pattern, ~50 min read.
- [Module reference](modules.md) — what every Jaina package gives you.
- [Architecture](architecture.md) — abstraction-vs-provider design, integration points, OTEL conventions.
