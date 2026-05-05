# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication style — Caveman mode (mandatory)

`caveman@caveman` plugin installed at user scope. Use **all** its skills and agents. Save tokens, keep accuracy.

**Default chat output**: caveman style — drop articles, fragments OK, no throat-clearing ("Let me", "I'll now"), no trailing summaries, status updates one line. Vietnamese same rule.

**Use these skills proactively** (don't wait for slash command):

| Skill | When |
|---|---|
| `caveman` | Always-on chat compression. Default mode = full. Drop to `lite` only for nuanced trade-off discussions. |
| `caveman-commit` | Every commit message. Conventional Commits, ≤50 char subject, body only when "why" is non-obvious. Auto-trigger on staged changes. |
| `caveman-review` | Every PR / diff review. One line per comment: `path:line 🔴 problem. fix.` No throat-clearing. |
| `compress` | Bloated CLAUDE.md / memory files. `/caveman:compress FILE` — overwrites with caveman; saves FILE.original.md. |

**Subagents** (use instead of inline work or vanilla `Explore`):

| Agent | When |
|---|---|
| `cavecrew-investigator` | Locate code, find usage of symbol, "where is X", spans >3 files |
| `cavecrew-builder` | Confined 1-2 file edit with clear spec — delegate, return compressed diff |
| `cavecrew-reviewer` | Diff/PR review at scale — one-liner per finding |

Subagent output is caveman-compressed → main context ~60% smaller. Prefer cavecrew over generic `Agent` when scope matches.

**Reach for fuller mode only**: explaining architecture decision, walking through trade-offs, or user explicitly asks for detail.

Trigger phrases that flip caveman intensity: `lite` / `full` / `ultra` / `wenyan` / `wenyan-ultra`. Stop with "stop caveman" / "normal mode".

## Build Commands

```bash
# Restore and build
dotnet restore Jaina.sln
dotnet build Jaina.sln
dotnet build Jaina.sln -c Release

# Run a specific sample
dotnet run --project samples/JainaShop/JainaShop.AppHost

# Run tests (once test projects exist)
dotnet test Jaina.sln
dotnet test tests/Jaina.<Module>.Tests/<project>.csproj  # single project
dotnet test --filter "FullyQualifiedName~ClassName"      # single test class
```

## Verification Approach

**Do NOT run `dotnet build Jaina.sln` as a verification step locally.** The solution targets `net8.0;net9.0;net10.0` but the local SDK may only support net8. The build will always fail with `NETSDK1045` for net9/net10 targets — this is an environment constraint, not a code error.

**Correct verification strategy by task type:**

| Task | How to verify |
|------|---------------|
| Code logic changes | Read the code — confirm correctness by inspection |
| New package added / version changed | Push to CI — GitHub Actions runs on .NET 9+ SDK |
| New project / .csproj added | Check that `.sln` includes it (`dotnet sln list`) |
| DI registration / API surface | Check the file compiles for `net8.0` only: `dotnet build <project>.csproj -f net8.0` |

CI is the authoritative build validator for this repository.

## Architecture

Jaina is a modular .NET 8 framework library organized into independent packages:

```
src/
  core/         Jaina.Core            — Guard, Result<T>, extensions, HttpClientBase
  aspnetcore/   Jaina.AspNetCore      — Problem Details, correlation ID, telemetry filters
  resilience/   Jaina.Resilience      — Polly v8 named pipelines (retry/timeout/CB/hedging)
  servicediscovery/ Jaina.ServiceDiscovery — Microsoft.Extensions.ServiceDiscovery wrapper
  multitenancy/ Jaina.MultiTenancy    — tenant resolver (header/claim/host/route) + middleware
  ratelimiting/ Jaina.RateLimiting    — per-IP / per-user / per-tenant / concurrency policies
  idempotency/  Jaina.Idempotency*    — IIdempotencyStore + InMemory/AspNetCore middleware
  caching/      Jaina.Caching*        — ICache abstraction + Memory/Redis/Fusion impls
  data/         Jaina.Data            — IRepository<T>, IUnitOfWork abstractions
                Jaina.Data.EfCore     — EF Core provider (EfRepository, EfUnitOfWork)
                Jaina.Data.Dapper     — Dapper provider (DapperRepository)
                Jaina.Data.Cqrs       — Command/Query buses, domain events, event store
  messaging/    Jaina.Messaging*      — IQueue<T>/ITopic<T> + RabbitMQ/ServiceBus/Broadcast
                Jaina.Messaging.Outbox* — transactional outbox + relay (InMemory; EfCore TBD)
                Jaina.Messaging.Inbox*  — consumer dedup (InMemory; Redis/EfCore TBD)
                Jaina.Messaging.Saga*   — orchestration + compensation (InMemory; EfCore/Redis TBD)
  storage/      Jaina.Storage*        — IFileStorage + Local/AzureBlob/FileShare/SFTP
  security/     Jaina.Security        — AES/RSA/BCrypt/JWT
                Jaina.Security.Authentication* — JWT bearer auth, Azure KeyVault
  observability/ Jaina.Observability*  — ITelemetry + AppInsights/ElasticAPM
  mapping/      Jaina.Mapping               — IMapper abstraction
                Jaina.Mapping.Mapster         — Mapster provider (AddJainaMapster)
  notifications/ Jaina.Notifications          — IEmailSender, ISmsSender abstractions
                Jaina.Notifications.Smtp        — SMTP provider (MailKit)
                Jaina.Notifications.ConsoleSms  — Console/logger SMS provider (dev/test)
samples/        Aspire AppHost, WebApi, Worker demos
tests/          xUnit projects for Core, Caching, Security, Data.Cqrs
```

Each functional area follows the same pattern: one abstraction package + one or more provider packages.

## Banned Packages

**Do NOT use or reference these packages anywhere in the solution:**

| Package | Reason | Replacement |
|---------|--------|-------------|
| `AutoMapper` | Commercial license (v13+) | `Jaina.Mapping.Mapster` — use `AddJainaMapster()` and inject `IMapper` |
| `FluentAssertions` | Commercial license (v7+) | `xunit` built-in `Assert.*` only |

If you see either package in any `.csproj` or `Directory.Packages.props`, remove it.

## Key Patterns

**Result pattern** (`Jaina.Core/Results/`): Use `Result` / `Result<T>` as return types instead of throwing exceptions for expected failures. Factory methods: `Result.Ok()`, `Result.Fail("msg")`.

**Guard clauses** (`Jaina.Core/Guard.cs`): Validate arguments at entry points. Methods: `Guard.NotNull()`, `Guard.NotNullOrEmpty()`, `Guard.Requires<TException>()`. Uses `CallerArgumentExpression` — no need to pass parameter name strings.

**DI registration**: Every module exposes `AddJaina<Feature>()` extension methods on `IServiceCollection`. Use `TryAddSingleton`/`TryAddScoped` so callers can override implementations.

**Repository / Unit of Work**: `IRepository<TEntity>` and `IUnitOfWork` abstractions in `Jaina.Data`. EF Core implementations (`EfRepository`, `EfUnitOfWork`, `AddJainaUnitOfWork<TContext>()`) in `Jaina.Data.EfCore`. Raw SQL via `DapperRepository` (`AddJainaDapper<TConnection>()`) in `Jaina.Data.Dapper`.

## Naming Conventions

- Projects: `Jaina.<Feature>` (abstraction) / `Jaina.<Feature>.<Provider>` (implementation)
- DI extensions: `AddJaina<Feature>()` (e.g., `AddJainaRedisCache()`, `AddJainaLocalStorage()`)
- Interfaces: `I` prefix (e.g., `ICache`, `IRepository<T>`, `IFileStorage`)
- Custom exceptions: `<Feature>Exception` (e.g., `FileStorageException`)

## Code Style

From `.editorconfig` and `Directory.Build.props`:
- File-scoped namespaces (`namespace Jaina.Module;`)
- `using` directives outside namespace, system-first sorted
- Nullable reference types enabled — annotate all APIs
- `TreatWarningsAsErrors=true` — no suppressions without justification
- LF line endings, UTF-8, 4-space indent (2 for JSON/XML/YAML)
- Target frameworks: both `$(LibTfms)` and `$(AppTfms)` resolve to `net8.0;net9.0;net10.0` — `netstandard2.0` is dropped. Use `$(LibTfms)` for abstraction packages, `$(AppTfms)` for provider packages

## Testing Conventions

**Test framework**: xUnit only. Use `Assert.*` from `Xunit` namespace — do **NOT** use FluentAssertions (changed to a commercial license in v7+).

**Pattern**: Always use AAA (Arrange / Act / Assert) with inline comments:

```csharp
[Fact]
public void MethodName_Condition_ExpectedBehavior()
{
    // Arrange
    var input = "hello";

    // Act
    var result = Guard.NotNullOrEmpty(input);

    // Assert
    Assert.Equal("hello", result);
}
```

**Naming**: `MethodName_Condition_ExpectedBehavior` (e.g. `Hash_EmptyInput_ThrowsHashingException`)

**Allowed packages in test projects**:
- `xunit` + `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`
- `NSubstitute` (for interfaces — avoid on contravariant generic interfaces like `ICommandHandler<in T>`, use concrete classes instead)
- `Microsoft.Extensions.DependencyInjection` (when testing DI wiring)

**Forbidden in tests**: `FluentAssertions`, `Shouldly`, or any assertion library with a commercial/proprietary license.

## Package Versioning

All NuGet versions are centralized in `Directory.Packages.props`. Do not specify versions in individual `.csproj` files — add the package reference without a version and let central management resolve it.

## Documentation Upkeep

After any task that adds, removes, renames, or restructures a package — update both files before considering the task done:

- **`README.md`**: Architecture table (src/ block), Installation snippet, and the relevant Module Usage code example
- **`CLAUDE.md`**: Architecture section and Key Patterns section

Common triggers: adding a provider package, splitting abstraction from implementation, renaming a DI method.
