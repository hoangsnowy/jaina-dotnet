# Jaina .NET

Modular enterprise framework for .NET 8 / 9 / 10. Microservice spine (Outbox · Inbox · Saga · Idempotency · Resilience · ServiceDiscovery), MultiTenancy + RateLimiting + Auth, Observability conventions, BackgroundJobs, gRPC, Testing fixtures — all wrapping `Microsoft.Extensions.*` so you don't fight the platform.

## Get started

```bash
dotnet add package Jaina.Core
dotnet add package Jaina.AspNetCore
# pick the modules you need; everything else stays out of your bin/
```

[Quick start →](getting-started.md) · [Architecture →](architecture.md) · [Cookbook →](blog/README.md)

## What's in the box

- [📘 **Ebook**: Building a production Orders service from scratch](blog/2026-05-05-orders-service-from-scratch.md) — 50-min walkthrough, every chapter ships runnable code + curl + reproducible failure scenario.
- [Module reference](modules.md) — what each `Jaina.*` package gives you, in one line per package.
- [API reference](api/) — generated from XML docs.

## Contributing

Source: [github.com/HoangSnowy/jaina-dotnet](https://github.com/HoangSnowy/jaina-dotnet)
