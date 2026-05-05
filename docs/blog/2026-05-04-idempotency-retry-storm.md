---
title: "Idempotency: surviving the mobile retry storm"
date: 2026-05-04
tags: [idempotency, http, microservices]
reading_time: "~6 min"
sample: samples/Jaina.Samples.WebApi/Program.cs
---

# Idempotency: surviving the mobile retry storm

## The Story

3:14 AM. The on-call phone buzzes — payment service is up, but every customer who placed an order on the mobile app at midnight just got charged twice. The mobile client has aggressive retries: any 5xx, network blip, or socket reset triggers an immediate replay of the same `POST /orders`. Tonight a 90-second TLS hiccup turned 1,200 orders into 2,800 charges. The refunds will take a week and the postmortem will take a month.

The fix is older than HTTP itself: **make the operation idempotent**. The client sends a stable `Idempotency-Key` header; the server caches the response against that key for some TTL. Replays return the cached result, side-effect-free.

## Naive approach

Most teams write something like this:

```csharp
app.MapPost("/api/orders", async (OrderRequest req, OrderService svc) =>
{
    var orderId = await svc.PlaceOrderAsync(req);  // charges card, writes DB row, sends email
    return Results.Created($"/api/orders/{orderId}", new { orderId });
});
```

What breaks:

- Two concurrent requests with the same body each get their own `orderId` and their own charge.
- A mobile retry that arrives 8 seconds later (after the original 504-ed at the load balancer but the server still completed) charges again.
- Anything before the first DB write gets duplicated when the client retries.

A "uniqueness check" by hashing the body is brittle: any whitespace difference, server-set timestamp, or correlation header defeats the hash.

## Jaina solution

```csharp
// Program.cs
builder.Services.AddJainaInMemoryIdempotency();   // dev/test; use Redis in prod
app.UseJainaIdempotency();                        // middleware reads Idempotency-Key header

app.MapPost("/api/orders", (OrderRequest req) =>
{
    var orderId = Guid.NewGuid();
    return Results.Created($"/api/orders/{orderId}",
        new { orderId, req.Sku, req.Quantity, createdAt = DateTimeOffset.UtcNow });
});
```

That's it. The middleware:

1. Reads the configured header (default `Idempotency-Key`) on `POST` / `PUT` / `PATCH` / `DELETE` only.
2. On a hit, replays the captured `2xx` response and adds `Idempotent-Replay: true` so observability tooling can count replays.
3. On a miss, buffers the response, lets the handler run, and stores any `2xx` for the configured TTL (default 24h).
4. Failed responses (`5xx`, `4xx`) are **not** cached — the client is supposed to retry those.

Source: [`IdempotencyMiddleware.cs`](../../src/idempotency/Jaina.Idempotency.AspNetCore/IdempotencyMiddleware.cs).

## Happy path

```bash
# First call — handler executes, response cached
$ curl -i -X POST http://localhost:5000/api/orders \
    -H "Idempotency-Key: mobile-7f3a2c-attempt-1" \
    -H "Content-Type: application/json" \
    -d '{"sku":"WIDGET-001","quantity":3}'
HTTP/1.1 201 Created
Content-Type: application/json
{"orderId":"d4...","sku":"WIDGET-001","quantity":3,"createdAt":"2026-05-04T10:30:00Z"}

# Second call, same key — handler does NOT execute, response replayed
$ curl -i -X POST http://localhost:5000/api/orders \
    -H "Idempotency-Key: mobile-7f3a2c-attempt-1" \
    -H "Content-Type: application/json" \
    -d '{"sku":"WIDGET-001","quantity":3}'
HTTP/1.1 201 Created
Idempotent-Replay: true
Content-Type: application/json
{"orderId":"d4...","sku":"WIDGET-001","quantity":3,"createdAt":"2026-05-04T10:30:00Z"}
```

Notice the **same `orderId`** and **same `createdAt`** — bytes-identical to the first response. The client cannot tell the difference except for the `Idempotent-Replay` header.

## Error scenarios

### 1. Network timeout between handler completion and client receipt

The client never sees the 201 because the connection was reset. It retries with the same key. The replay returns the original cached body with status 201 — no double-charge.

### 2. Two concurrent requests with the same key (race)

Both arrive within milliseconds, both miss the cache, both run. With the in-memory store this is technically possible — the second writer overwrites the first entry, but each client still gets one response. **For production, use a Redis-backed store with `SETNX` semantics** (lands in a follow-up commit). Document this gotcha loudly: in-memory ≠ distributed.

### 3. Handler returns 500

```bash
$ curl -X POST .../api/orders -H "Idempotency-Key: oops" ...   # 500
$ curl -X POST .../api/orders -H "Idempotency-Key: oops" ...   # handler runs again
```

The middleware refuses to cache non-2xx responses. The retry actually retries. Important: if your handler has *partial* side effects before throwing (e.g. card charged but DB write failed), the second attempt will try to charge again. **Idempotency at the HTTP layer is not a substitute for idempotency at the business layer** — combine with the Outbox pattern (next post).

### 4. Same key, different body

The middleware does not currently validate that the body matches the cached request. A malicious or buggy client sending `{"sku":"A","qty":1}` followed by `{"sku":"B","qty":1000}` with the same key will get the cached response for the *first* request. Mitigations: (a) include a body hash in the cached entry and 422 on mismatch, (b) make the client derive the key from the body. The first is on the roadmap.

### 5. GET with an `Idempotency-Key` header

Ignored. GET / HEAD are excluded by `IdempotencyOptions.CacheableMethods` because they're already idempotent. The header is allowed to flow through harmlessly.

## What you'd see in production

```
[10:30:00] POST /api/orders → 201 (handler ran, key=mobile-7f3a2c-attempt-1)
[10:30:08] POST /api/orders → 201 (replay, key=mobile-7f3a2c-attempt-1, Idempotent-Replay: true)
```

Add a counter on the `Idempotent-Replay` response header in your gateway to chart your replay rate. Sudden spikes mean a client is retrying too aggressively or your upstream is timing out.

## Trade-offs & gotchas

- **TTL choice**: 24h is a reasonable default. Longer means more storage; shorter means clients on flaky networks can re-charge after a long outage.
- **Replay does not re-run side effects**, including expensive ones like SMS/email. If your handler sends an email and you want re-sends to behave differently, idempotency is the wrong layer — handle it explicitly in the email subsystem.
- **Cache poisoning**: a cached 201 with a real `orderId` becomes stale if you delete that order. Replays will return a 201 referencing a non-existent resource. Either invalidate the idempotency entry on cancel, or shorten the TTL.
- **Client must generate stable keys**. Random GUID per attempt defeats the pattern. Anchor on the user intent (cart hash + timestamp + device id, etc.).

## Try it yourself

```bash
git clone https://github.com/HoangSnowy/jaina-dotnet
cd jaina-dotnet
dotnet run --project samples/Jaina.Samples.WebApi

# In another terminal
KEY=demo-$(date +%s)
for i in 1 2 3; do
  curl -i -X POST http://localhost:5000/api/orders \
    -H "Idempotency-Key: $KEY" \
    -H "Content-Type: application/json" \
    -d '{"sku":"DEMO","quantity":1}'
  echo
done
```

You will see the first response with `201 Created`, and the next two with `201 Created` + `Idempotent-Replay: true` — same body each time.

## Further reading

- Source: [`Jaina.Idempotency.AspNetCore/IdempotencyMiddleware.cs`](../../src/idempotency/Jaina.Idempotency.AspNetCore/IdempotencyMiddleware.cs)
- Tests (5 cases including failure paths): [`Jaina.Idempotency.Tests/IdempotencyMiddlewareTests.cs`](../../tests/Jaina.Idempotency.Tests/IdempotencyMiddlewareTests.cs)
- Next post: [Outbox: never lose another order on Black Friday](2026-05-04-outbox-black-friday.md) — pairs naturally with idempotency for end-to-end reliability.
