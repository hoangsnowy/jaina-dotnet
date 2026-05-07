# Modules — at a glance

One line per package. Click through for details.

## Core + AspNetCore

| Package | What it gives you |
|---|---|
| [`Jaina.Core`](#) | `Result<T>` + `IResult` — shared kernel for the Result pattern across modules |
| [`Jaina.AspNetCore`](#) | `AddJainaProblemDetails()`, `UseJainaPipeline()`, `WithJainaResultFilter()` |

## Microservice spine

| Package | What it gives you |
|---|---|
| [`Jaina.Resilience`](blog/2026-05-05-resilience-pipelines.md) | 4 named pipelines: `default`, `queue-publish`, `external-http`, `database` |
| [`Jaina.ServiceDiscovery`](blog/2026-05-05-service-discovery.md) | `AddJainaHttpClient<T>(name)` wires service discovery + resilience + tenant header in one call |
| [`Jaina.Idempotency`](blog/2026-05-04-idempotency-retry-storm.md) | `Idempotency-Key` HTTP middleware; `.InMemory` + `.Redis` providers |
| [`Jaina.Messaging.Outbox`](blog/2026-05-04-outbox-black-friday.md) | Transactional outbox + relay; `.InMemory` + `.EfCore` providers |
| [`Jaina.Messaging.Inbox`](#) | Consumer-side dedup; `.InMemory` + `.Redis` + `.EfCore` providers |
| [`Jaina.Messaging.Saga`](blog/2026-05-05-saga-orchestration.md) | Orchestration saga + reverse compensation; `.InMemory` + `.EfCore` + `.Redis` providers |

## Multi-tenant + AuthN/Z + Observability

| Package | What it gives you |
|---|---|
| [`Jaina.MultiTenancy`](blog/2026-05-05-multi-tenancy.md) | `ITenantContext` + Header/Claim/Host/Route resolvers + middleware |
| [`Jaina.RateLimiting`](#) | 4 named policies: `per-ip`, `per-user`, `per-tenant`, `concurrency` |
| [`Jaina.Security.Authentication`](blog/2026-05-05-grpc-jwt-tenant.md) | JWT bearer + ApiKey scheme + `IUserContext` + scope policy DSL |
| [`Jaina.Observability`](blog/2026-05-05-observability-traces.md) | `JainaActivitySource` + `JainaMeter` + `TagConventions` for uniform OTEL |

## Productivity

| Package | What it gives you |
|---|---|
| [`Jaina.Validation`](#) | FluentValidation endpoint filter — auto 400 ProblemDetails |
| [`Jaina.HealthChecks`](blog/2026-05-05-health-checks.md) | `/health/live` + `/health/ready` with the live/ready tag convention |
| [`Jaina.BackgroundJobs`](blog/2026-05-05-background-jobs.md) | `IBackgroundJobScheduler` + Quartz provider for one-shot + cron |
| [`Jaina.Grpc`](blog/2026-05-05-grpc-jwt-tenant.md) | gRPC server interceptors — logging, correlation |
| [`Jaina.Testing`](#) | `JainaWebApplicationFactory<T>` + `FakeClock` + Testcontainers fixtures |

## Storage / Caching / Data / Messaging / Notifications

| Package | What it gives you |
|---|---|
| `Jaina.Caching` (+ `.Memory`, `.Redis`, `.Fusion`) | `ICache` with three provider choices |
| `Jaina.Data` (+ `.EfCore`, `.Dapper`, `.Cqrs`) | `IRepository<T>`, `IUnitOfWork`, command/query buses |
| `Jaina.Messaging` (+ `.RabbitMQ`, `.AzureServiceBus`) | `IQueue<T>` + `ITopic<T>` |
| `Jaina.Storage` (+ `.Local`, `.AzureBlob`, `.Sftp`) | `IFileStorage` |
| `Jaina.Notifications` + `.Smtp` | `IEmailSender`, `ISmsSender` |
| `Jaina.Security` (+ `.KeyVault`) | AES / RSA / BCrypt / SHA / JWT helpers |

## Sample app

[`samples/JainaShop`](https://github.com/HoangSnowy/jaina-dotnet/tree/main/samples/JainaShop) — five microservices wired with the patterns above plus an Aspire AppHost.

| Service | Demonstrates |
|---|---|
| `JainaShop.Catalog` | Caching + EF Core + HealthChecks + Observability |
| `JainaShop.Orders` | Outbox.EfCore + Idempotency + relay |
| `JainaShop.Identity` | JWT issuer + ApiKey scheme + tenant claim |
| `JainaShop.Notifier` | Inbox dedup + sample ConsoleSms (background scheduler swap to Hangfire — Phase 1) |
| `JainaShop.Gateway` | RateLimiting + MultiTenancy + ServiceDiscovery + HttpClient forwarding |
| `JainaShop.AppHost` | Aspire orchestration of all 5 + Redis |

```bash
dotnet run --project samples/JainaShop/JainaShop.AppHost
```
