# JainaShop — End-to-end Jaina sample

A small e-commerce slice that wires together the Jaina framework patterns. Each service in the shop demonstrates a distinct subset of the framework so you can see how the pieces fit together in a realistic-but-readable codebase.

## Services

| Service | Status | Demonstrates |
|---|---|---|
| **JainaShop.Catalog** | ✅ shipped | Caching (Memory), EF Core (InMemory provider), HealthChecks (live/ready), Observability (`JainaActivitySource` + tag conventions), Result→IResult filter |
| JainaShop.Identity | TBD | JWT issuer + ApiKey scheme + scope policies |
| JainaShop.Orders | TBD | CQRS + Outbox (EF Core) + Saga (Payment + Shipping rollback) + Idempotency |
| JainaShop.Gateway | TBD | RateLimiting + MultiTenancy + ServiceDiscovery + Resilience |
| JainaShop.Notifier | TBD | BackgroundJobs (Quartz) + Inbox dedup |
| JainaShop.AppHost | TBD | Aspire orchestration: spin everything + Postgres + Redis + RabbitMQ |

## Run a single service

```bash
dotnet run --project samples/JainaShop/JainaShop.Catalog
```

Defaults to `http://localhost:5000` (or whichever port ASP.NET picks). Swagger is enabled in Development.

## Catalog endpoints

```bash
# List (cached for 30s after first hit)
curl http://localhost:5000/products

# Single (cached for 5 min after first read)
curl http://localhost:5000/products/{id}

# Create — invalidates the list cache
curl -X POST http://localhost:5000/products \
  -H "Content-Type: application/json" \
  -d '{"sku":"NEW-001","name":"New Thing","price":4.99,"stock":10}'

# Health probes
curl http://localhost:5000/health/live   # process responsive
curl http://localhost:5000/health/ready  # downstream deps OK
```

## Observability

Every endpoint emits a span via `JainaActivitySource`. Subscribe in your OTEL setup:

```csharp
otel.AddSource(JainaActivitySource.Name);
```

Span names: `jaina.catalog.products.list`, `jaina.catalog.products.get`, `jaina.catalog.products.create`. Tags include `jaina.cache.hit` (true/false) and `jaina.cache.key` per [`TagConventions`](../../src/observability/Jaina.Observability/Telemetry/TagConventions.cs).

## What lands next

Identity → Orders → Gateway → Notifier → AppHost. Each one ships its own commit with a focused demo. Once all five run together under `JainaShop.AppHost` (Aspire), the cookbook gets a final post showing the full request flow with traces.
