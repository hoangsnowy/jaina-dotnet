---
title: "Service discovery: from hard-coded URLs to tenant-aware routing"
date: 2026-05-05
tags: [servicediscovery, microservices, kubernetes]
reading_time: "~5 min"
sample: samples/JainaShop/JainaShop.Gateway/Program.cs
---

# Service discovery: from hard-coded URLs to tenant-aware routing

## The Story

Wednesday afternoon. The DevOps team flips the order service to a new replica set in a different namespace. The CIDR changes. Half the gateway pods still cache the old IP and the other half resolve via DNS — for ten minutes, every other request is a 502 while DNS TTLs expire. Nobody owns the runbook for "which gateway is talking to which orders".

Hard-coded `http://orders-service.production.svc.cluster.local:8080` paths are everywhere. So is the bug.

Service discovery solves this. **Microsoft.Extensions.ServiceDiscovery** ships with .NET 8+, supports configuration / DNS / Kubernetes resolvers, and integrates with `HttpClient`. `Jaina.ServiceDiscovery` is a thin opinionated wrapper.

## Naive approach

```csharp
services.AddHttpClient<IOrdersClient, OrdersClient>(c =>
    c.BaseAddress = new Uri("http://orders-service.production.svc.cluster.local:8080"));
```

What breaks:

- Hardcoded host means staging and production differ — env-specific config sprawls
- DNS TTL stale → some pods see the old endpoint
- A subset of tenants needs a dedicated cluster (compliance, latency) — no clean way to override

## Jaina solution

```csharp
// Program.cs
builder.Services.AddJainaServiceDiscovery();
builder.Services.AddHttpClient("orders", c => c.BaseAddress = new("http://orders"));
```

`http://orders` is a logical name. The resolver chain (configuration → DNS → kube) turns it into an endpoint at request time.

`appsettings.Production.json`:

```json
{
  "Services": {
    "orders": { "http": [ "https://orders.svc.cluster.local" ] }
  }
}
```

`appsettings.Development.json`:

```json
{
  "Services": {
    "orders": { "http": [ "http://localhost:5102" ] }
  }
}
```

Same code, different envs. Source: [`Jaina.ServiceDiscovery/ServiceCollectionExtensions.cs`](../../src/servicediscovery/Jaina.ServiceDiscovery/ServiceCollectionExtensions.cs).

## Happy path

```bash
$ curl http://localhost:5000/api/products
[ ... cached list from catalog upstream ... ]
```

Gateway resolved `http://catalog` → `http://localhost:5101` from configuration, called the upstream, returned the body.

## Error scenarios

### 1. Upstream replaced — endpoint changed

Configuration / DNS picks up the new address on next request. No restart needed. The brief in-flight failures are caught by `Jaina.Resilience` retry + circuit breaker (compose with `services.AddJainaResilience()` and the `StandardResilienceHandler` baked into ServiceDefaults).

### 2. All replicas of upstream are down

Resolver returns the configured endpoint(s); the HTTP call fails. With `AddStandardResilienceHandler` the gateway retries with exponential backoff, then trips the circuit breaker. Subsequent calls fail fast (`BrokenCircuitException`) instead of hammering the dead service.

### 3. Logical name not configured

```
InvalidOperationException: No endpoint configured for service 'orders'
```

Fail-fast at first call rather than mysterious 404. Easy to spot in logs and fix in `appsettings.json`.

### 4. Tenant-specific routing

```json
{
  "Services": {
    "orders":         { "http": [ "https://orders.shared.svc" ] },
    "orders.acme":    { "http": [ "https://orders.acme.svc" ] }
  }
}
```

Custom resolver maps `http://orders` → `orders.{tenantId}` if the dedicated entry exists, else falls back. Lands as a follow-up; today, build it as a custom `IServiceEndpointProvider` against `ITenantContext`.

### 5. Multi-region with health-aware preference

Define multiple endpoints in configuration; the round-robin resolver picks one. Combine with `Jaina.HealthChecks` so an unhealthy region drops out of rotation.

## What you'd see in production

OTEL trace tags from `HttpClientFactory`:

```
http.client.duration  150ms
  url.full             http://orders/orders
  server.address       orders.svc.cluster.local
  server.port          443
  http.response.status 201
```

`server.address` is the **resolved** endpoint, not the logical name — useful when triaging "which replica did this hit?".

## Trade-offs & gotchas

- **Service discovery without resilience is a footgun.** Always pair with `Jaina.Resilience` so a stale endpoint doesn't take down your callers.
- **Configuration overrides DNS.** Don't accidentally pin a bad endpoint in `appsettings.Production.json` and forget about it.
- **TLS ↔ logical names.** When the resolver picks an IP, your TLS cert must still match the logical hostname. Configure `HttpClient` with `RequestUri.Host = "orders"` and use SNI-aware certs.

## Try it yourself

```bash
dotnet run --project samples/JainaShop/JainaShop.AppHost
# Aspire wires services in-process; gateway resolves catalog/orders via configuration

curl http://localhost:5000/api/products
curl -H "X-Tenant: acme" -X POST http://localhost:5000/api/orders \
     -H "Content-Type: application/json" \
     -d '{"sku":"WIDGET","quantity":1,"unitPrice":9.99}'
```

## Further reading

- Source: [`Jaina.ServiceDiscovery/ServiceCollectionExtensions.cs`](../../src/servicediscovery/Jaina.ServiceDiscovery/ServiceCollectionExtensions.cs)
- Tests: [`JainaServiceDiscoveryTests.cs`](../../tests/unit/Jaina.ServiceDiscovery.UnitTests/JainaServiceDiscoveryTests.cs)
- Companion post: [Resilience pipelines](2026-05-05-resilience-pipelines.md)
