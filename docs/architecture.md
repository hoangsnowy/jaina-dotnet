# Architecture

## Design rules

1. **Wrap, don't replace.** Every Jaina module sits on top of `Microsoft.Extensions.*` (Resilience, ServiceDiscovery, FeatureManagement, RateLimiting, Caching, Localization). You can drop into the underlying API at any point.
2. **Abstraction in `Jaina.X`, providers in `Jaina.X.<Provider>`.** Swap providers by changing one DI registration; your application code never changes.
3. **Every provider emits OTEL traces + metrics by default.** `JainaActivitySource` (source name `Jaina`) and `JainaMeter` are the single subscription points.
4. **`Result<T>` all the way to HTTP.** `Jaina.Core.Results.Result<T>` flows from domain through to a `Microsoft.AspNetCore.Http.IResult` via `WithJainaResultFilter()`.
5. **Test infra is a first-class module.** `Jaina.Testing` + `Jaina.Testing.Containers` ship Testcontainers fixtures so providers ship with real-DB integration tests, not just InMemory unit tests.

## Module groups

```
src/
  core/            Jaina.Core              Guard · Result<T> · extensions · HttpClientBase
  aspnetcore/      Jaina.AspNetCore        ProblemDetails · CorrelationIdFilter · Result→IResult filter · UseJainaPipeline
  resilience/      Jaina.Resilience        Polly v8 named pipelines (default · queue-publish · external-http · database)
  servicediscovery/ Jaina.ServiceDiscovery Tenant-aware resolver + AddJainaHttpClient<T> all-in-one wiring
  multitenancy/    Jaina.MultiTenancy      Header/Claim/Host/Route resolvers + ITenantContext + middleware
  ratelimiting/    Jaina.RateLimiting      Per-IP / per-user / per-tenant / concurrency policies
  idempotency/     Jaina.Idempotency*      IIdempotencyStore + InMemory/Redis stores + ASP.NET middleware
  caching/         Jaina.Caching*          ICache + Memory/Redis/Fusion providers
  data/            Jaina.Data*             IRepository<T> + IUnitOfWork + EF Core / Dapper providers + CQRS bus
  messaging/       Jaina.Messaging*        IQueue<T>/ITopic<T> + RabbitMQ/ServiceBus/Broadcast
                   Jaina.Messaging.Outbox* Transactional outbox + relay (InMemory + EfCore)
                   Jaina.Messaging.Inbox*  Consumer dedup (InMemory + Redis + EfCore)
                   Jaina.Messaging.Saga*   Orchestration saga + reverse compensation (InMemory + EfCore + Redis)
  storage/         Jaina.Storage*          IFileStorage + Local/AzureBlob/AzureFileShare/SFTP
  security/        Jaina.Security          AES/RSA/BCrypt/SHA + JWT helpers
                   Jaina.Security.Authentication  JWT bearer + ApiKey + IUserContext + scope policies
                   Jaina.Security.KeyVault Azure Key Vault
  observability/   Jaina.Observability     ITelemetry/ISpan + JainaActivitySource + JainaMeter + TagConventions
                   Jaina.Observability.ApplicationInsights / .ElasticApm
  mapping/         Jaina.Mapping*          IMapper + Mapster provider
  notifications/   Jaina.Notifications*    IEmailSender/ISmsSender + SMTP / Console SMS
  validation/      Jaina.Validation        FluentValidation endpoint filter (400 ProblemDetails)
  healthchecks/    Jaina.HealthChecks      /health/live + /health/ready (live/ready tag convention)
  backgroundjobs/  Jaina.BackgroundJobs*   IBackgroundJobScheduler + Quartz provider
  grpc/            Jaina.Grpc              gRPC server + interceptors (logging + correlation)
  testing/         Jaina.Testing*          JainaWebApplicationFactory + FakeClock + Testcontainers fixtures
```

## How the modules connect

```
                          +-------------------------+
                          |   Jaina.MultiTenancy    |
                          |   ITenantContext        |
                          +-------------------------+
                              |               |
                              v               v
+------------------+   +-------------------------+   +------------------+
| Jaina.RateLimit  |   | Jaina.ServiceDiscovery  |   | Jaina.FeatureFlg |
| per-tenant       |   | tenant-aware resolver   |   | TenantTargeting  |
| partition        |   | + tenant header propag. |   | filter           |
+------------------+   +-------------------------+   +------------------+
                              |
                              v
                       +---------------------+
                       | Jaina.Resilience    |  ----+
                       | (named pipelines)   |      |
                       +---------------------+      |
                              |                    |
                              v                    v
                       +-------------------------------+
                       | HttpClient outbound           |
                       | (single AddJainaHttpClient<T> |
                       |  call wires all of the above) |
                       +-------------------------------+

+---------------------+    +------------------------+    +------------------+
| Jaina.Idempotency   |    | Jaina.Messaging.Outbox |    | Jaina.Messaging  |
| AspNetCore          |    | (atomic with domain    |    | .Saga            |
| middleware          |    |  write via EF Core)    |    | orchestration    |
+---------------------+    +------------------------+    +------------------+
        |                          |                              |
        |                          v                              v
        |                    +--------------------------------------+
        |                    | broker (RabbitMQ / SB / Kafka)       |
        |                    | -> Jaina.Messaging.Inbox dedup       |
        |                    | -> Saga consumes, runs steps,        |
        |                    |    compensates on failure            |
        |                    +--------------------------------------+
        |
        v
   +------------------+        +------------------------+
   | Jaina.Observ.    |  <---- | every provider above   |
   | JainaActivitySrc |        | emits jaina.* spans    |
   | + TagConventions |        | through this source    |
   +------------------+        +------------------------+
```

## OTEL conventions

| Span name | Module | Tags |
|---|---|---|
| `jaina.cache.get` / `set` / `remove` | Caching | `cache.key`, `cache.hit`, `cache.provider` |
| `jaina.outbox.relay.tick` | Messaging.Outbox | `outbox.batch_size` |
| `jaina.outbox.dispatch` | Messaging.Outbox | `message.id`, `message.type`, `destination`, `outbox.attempt` |
| `jaina.saga.run` | Messaging.Saga | `saga.correlation_id` |
| `jaina.saga.step` | Messaging.Saga | `saga.correlation_id`, `saga.step` |
| `jaina.idempotency.evaluate` | Idempotency.AspNetCore | `idempotency.key`, `idempotency.replay` |
| `jaina.featureflag.evaluate` | FeatureFlags | `featureflag.name`, `featureflag.enabled` |
| `jaina.localization.lookup` | Localization | `localization.key`, `localization.tenant`, `localization.found` |

Subscribe once:

```csharp
otel.WithTracing(t => t.AddSource(JainaActivitySource.Name))
    .WithMetrics(m => m.AddMeter(JainaMeter.Name));
```

## Cross-cutting policy

- `IUserContext` (Jaina.Security.Authentication) and `ITenantContext` (Jaina.MultiTenancy) are scoped per request; every module reads them rather than reaching for `HttpContext` directly.
- `Result<T>` failures map to ProblemDetails with the same status/title rules whether raised by Validation, Saga, or domain code.
- The CQRS bus (Jaina.Data.Cqrs) is where pipeline behaviors layer (logging → validation → transaction → idempotency → outbox → retry).

For a full walkthrough of how these compose, see the [📘 Ebook](blog/2026-05-05-orders-service-from-scratch.md).
