# Jaina Cookbook — Real-world recipes

Real production patterns, real failure modes, runnable sample code. Every post follows the same template:

1. **The Story** — a real business problem (Black Friday peak, retry storm, partial failure)
2. **Naive approach** — what most teams write, and why it breaks
3. **Jaina solution** — code from `samples/Jaina.Samples.WebApi`, copy-pasteable
4. **Happy path** — how it looks when everything works
5. **Error scenarios** — at minimum 4 failure modes with how Jaina handles each
6. **What you'd see in production** — logs / OTEL traces / metrics
7. **Trade-offs & gotchas** — the honest fine print
8. **Try it yourself** — `dotnet run` + curl scripts

## Index

| # | Post | Patterns | Status |
|---|------|---------|--------|
| 1 | [Idempotency: surviving the mobile retry storm](2026-05-04-idempotency-retry-storm.md) | Idempotency middleware | ✅ |
| 2 | [Outbox: never lose another order on Black Friday](2026-05-04-outbox-black-friday.md) | Messaging.Outbox, relay | ✅ |
| 3 | Resilience pipelines: 5 Polly patterns in 3 lines | Resilience | TBD |
| 4 | Service discovery: from hard-coded URLs to tenant-aware routing | ServiceDiscovery | TBD |
| 5 | Saga orchestration: rolling back across Payment + Shipping | Messaging.Saga | TBD (after M1 saga lands) |
| 6 | Reading OTEL traces like a novel: distributed bug detective work | Observability | TBD (after M2) |
| 7 | Health checks that don't fool Kubernetes: live vs ready vs startup | HealthChecks | TBD (after M3) |
| 8 | gRPC + JWT + tenant: auth flow between microservices | Grpc, Auth, MultiTenancy | TBD (after M2/M3) |
| 9 | Background jobs that survive 1M-row reprocessing | BackgroundJobs, Outbox | TBD (after M3) |
| 10 | Migrating monolith → microservices, one slice at a time | Strangler fig, Outbox, ServiceDiscovery | TBD (after M3) |

## Conventions

- **Error scenarios are mandatory.** Every post lists at least 4 failure modes.
- Code in posts must come from `samples/Jaina.Samples.WebApi` or a `samples/blog/<slug>/` branch — readers should be able to `git checkout` and run.
- Screenshots of OTEL traces / Grafana panels should be from real runs, not fabricated.
- Each post links its source sample and tests so reviewers can verify claims.

See [`_template.md`](_template.md) for the post skeleton.
