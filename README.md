# Jaina .NET

Modular enterprise framework for .NET 10. Microservice patterns (Outbox · Inbox · Saga · Idempotency · Resilience · ServiceDiscovery), MultiTenancy + Auth + RateLimiting, Observability conventions, BackgroundJobs, gRPC, Testing fixtures — all wrapping `Microsoft.Extensions.*` so you don't fight the platform.

[![Build](https://img.shields.io/github/actions/workflow/status/HoangSnowy/jaina-dotnet/ci.yml?branch=main)](https://github.com/HoangSnowy/jaina-dotnet/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/docs-jaina--dotnet-blue)](https://hoangsnowy.github.io/jaina-dotnet/)

```bash
dotnet add package Jaina.Core
dotnet add package Jaina.AspNetCore
# pick the modules you need
```

## Documentation

- 🚀 **[Getting started](docs/getting-started.md)** — stand up a service with Idempotency + Outbox + HealthChecks in 10 minutes
- 🏛️ **[Architecture](docs/architecture.md)** — design rules, module map, OTEL conventions, integration diagram
- 📦 **[Module reference](docs/modules.md)** — what every Jaina package gives you, one line each
- 📘 **[Cookbook](docs/blog/README.md)** — runnable recipes per pattern, each with happy path + error scenarios
  - **[📘 Ebook: Building a production Orders service from scratch](docs/blog/2026-05-05-orders-service-from-scratch.md)** — 50-min walkthrough with reproducible failure modes
- 🛠️ **[Sample app](samples/JainaShop/README.md)** — `JainaShop` (5 microservices + Aspire AppHost)
- 📊 **[Benchmarks](tests/benchmarks/Jaina.Benchmarks/README.md)** — BenchmarkDotNet harness for hot paths
- 🧪 **[Tests](tests/README.md)** — unit vs integration suite convention

## Live docs site

Generated with **DocFX**, deployed to GitHub Pages on every push to `main`:

→ https://hoangsnowy.github.io/jaina-dotnet/

Build locally:

```bash
dotnet tool update -g docfx
docfx docfx.json --serve
# Open: http://localhost:8080
```

## Contributing

PRs welcome. See [`CLAUDE.md`](CLAUDE.md) for the codebase conventions (caveman commit style, no FluentAssertions, AAA tests, etc.).

## License

MIT — see [`LICENSE`](LICENSE).
