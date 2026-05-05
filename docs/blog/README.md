# Jaina Cookbook — Real-world recipes

Real production patterns, real failure modes, runnable sample code. Every post follows the same template:

1. **The Story** — a real business problem (Black Friday peak, retry storm, partial failure)
2. **Naive approach** — what most teams write, and why it breaks
3. **Jaina solution** — code from `samples/JainaShop/JainaShop.AppHost`, copy-pasteable
4. **Happy path** — how it looks when everything works
5. **Error scenarios** — at minimum 4 failure modes with how Jaina handles each
6. **What you'd see in production** — logs / OTEL traces / metrics
7. **Trade-offs & gotchas** — the honest fine print
8. **Try it yourself** — `dotnet run` + curl scripts

## Index

| # | Post | Patterns | Status |
|---|------|---------|--------|
| 0 | [📘 **EBOOK** — From hello-world to Black Friday: building a production Orders service](2026-05-05-orders-service-from-scratch.md) | Idempotency + Outbox + Saga + Observability — full walkthrough, ~50 min | ✅ |
| 1 | [Idempotency: surviving the mobile retry storm](2026-05-04-idempotency-retry-storm.md) | Idempotency middleware | ✅ |
| 2 | [Outbox: never lose another order on Black Friday](2026-05-04-outbox-black-friday.md) | Messaging.Outbox, relay | ✅ |
| 3 | [Resilience pipelines: 5 Polly patterns in 3 lines](2026-05-05-resilience-pipelines.md) | Resilience | ✅ |
| 4 | [Service discovery: from hard-coded URLs to tenant-aware routing](2026-05-05-service-discovery.md) | ServiceDiscovery | ✅ |
| 5 | [Saga orchestration: Payment + Shipping rollback](2026-05-05-saga-orchestration.md) | Messaging.Saga | ✅ |
| 5b | [Multi-tenant SaaS: shared schema, row-level isolation](2026-05-05-multi-tenancy.md) | MultiTenancy + EF query filters | ✅ |
| 6 | [Reading OTEL traces like a novel: distributed bug detective work](2026-05-05-observability-traces.md) | Observability | ✅ |
| 7 | [Health checks that don't fool Kubernetes: live vs ready vs startup](2026-05-05-health-checks.md) | HealthChecks | ✅ |
| 8 | [gRPC + JWT + tenant: auth flow between microservices](2026-05-05-grpc-jwt-tenant.md) | Grpc, Auth, MultiTenancy | ✅ |
| 9 | [Background jobs that survive 1M-row reprocessing](2026-05-05-background-jobs.md) | BackgroundJobs, Outbox | ✅ |
| 10 | [Migrating monolith → microservices, one slice at a time](2026-05-05-monolith-to-microservices.md) | Strangler fig, Outbox, ServiceDiscovery | ✅ |

## Conventions

- **Error scenarios are mandatory.** Every post lists at least 4 failure modes.
- Code in posts must come from `samples/JainaShop/JainaShop.AppHost` or a `samples/blog/<slug>/` branch — readers should be able to `git checkout` and run.
- Screenshots of OTEL traces / Grafana panels should be from real runs, not fabricated.
- Each post links its source sample and tests so reviewers can verify claims.

See [`_template.md`](_template.md) for the post skeleton.
