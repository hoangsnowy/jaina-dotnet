# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication style — Caveman mode (MANDATORY, enforced)

`caveman@caveman` plugin installed at user scope. **Every** skill + agent MUST be invoked when its trigger condition fires. Skipping = bug.

**Chat output**: full caveman by default. Drop articles, fragments OK, no throat-clearing, no trailing summaries. Vietnamese same rule.

### Triggers (NOT optional)

| Trigger | Tool to invoke | Output shape |
|---|---|---|
| Writing a commit message (any `git commit`) | `caveman-commit` skill | Conventional Commits. Subject ≤50 chars. Body only if *why* is non-obvious — most commits = subject only. NO multi-paragraph essays. |
| Reviewing a diff / PR / file (any "review", "audit this", PR feedback) | `caveman-review` skill | `path:L<n>: <emoji> <severity>: problem. fix.` per finding. 🔴 bug 🟡 risk 🔵 nit ❓ q. |
| File >150 lines that's mostly prose (CLAUDE.md, blog post, memory file) bloating context | `compress` skill | `/caveman:compress FILE` — overwrites caveman, backs up `FILE.original.md`. |
| Locate code, "where is X", "what calls Y", spans >2 files | `cavecrew-investigator` agent | NOT vanilla `Explore`. Output is pre-compressed. |
| Confined 1-2 file edit with clear spec | `cavecrew-builder` agent | NOT inline edit. Delegate; return compressed diff. |
| Diff/PR review at scale (>5 files) | `cavecrew-reviewer` agent | One-liner per finding, no throat-clearing. |

### Hard rules

1. **Commit messages**: ≤50 char subject. Body forbidden unless commit fixes a non-obvious bug or makes a load-bearing decision. Multi-paragraph commit messages = caveman violation.
2. **Diff reviews**: never write prose paragraphs. Output `caveman-review` format only.
3. **Multi-file work**: invoke `cavecrew-*` agent. Inline = lazy.
4. **Bloated files** (>500 lines mostly prose): run `compress` before adding more.

### When fuller mode is allowed

Architecture decisions, trade-off walkthroughs, security warnings, irreversible action confirmations. User asks for detail. **Otherwise: caveman.**

Trigger phrases flip intensity: `lite` / `full` / `ultra` / `wenyan` / `wenyan-ultra`. Stop with `stop caveman` / `normal mode`.

### Self-check before responding

- Chat reply >5 lines and not architecture/security? → compress
- About to commit? → invoke `caveman-commit`
- About to review? → invoke `caveman-review`
- Edit spans >2 files? → consider `cavecrew-builder`
- Searching code? → use `cavecrew-investigator`, not inline grep loops

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

Solution multi-targets `net8.0;net9.0;net10.0` (TFMs centralised via `LibTfms` / `AppTfms` in `Directory.Build.props`). Local build works only for the SDKs you have installed (`dotnet --list-sdks`); CI on GitHub Actions remains the authoritative validator — push and check.

| Task | How to verify |
|------|---------------|
| Code logic changes | Read the code — confirm correctness by inspection |
| Package add / version bump | Local `dotnet restore` + `dotnet build` if SDK 10 present; otherwise push CI |
| New project / .csproj added | Check `Jaina.slnx` + relevant `*.slnf` include it |
| DI registration / API surface | Local `dotnet build <project>.csproj` |

## Architecture

Jaina is a modular .NET 10 framework library organized into independent packages:

```
src/
  core/         Jaina.Core            — Result<T> + IResult (shared kernel)
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
  messaging/    Jaina.Messaging*      — IQueue<T>/ITopic<T> + RabbitMQ/ServiceBus
                Jaina.Messaging.Outbox* — transactional outbox + relay (InMemory + EfCore)
                Jaina.Messaging.Inbox*  — consumer dedup (InMemory + Redis + EfCore)
                Jaina.Messaging.Saga*   — orchestration + compensation (InMemory + EfCore + Redis)
  storage/      Jaina.Storage*        — IFileStorage + Local/AzureBlob/SFTP
  security/     Jaina.Security        — AES/RSA/BCrypt/JWT
                Jaina.Security.Authentication* — JWT bearer auth, Azure KeyVault
  observability/ Jaina.Observability*  — ITelemetry + AppInsights
  notifications/ Jaina.Notifications          — IEmailSender, ISmsSender abstractions
                Jaina.Notifications.Smtp        — SMTP provider (MailKit)
samples/        Aspire AppHost, WebApi, Worker demos
tests/          xUnit projects for Core, Caching, Security, Data.Cqrs
```

Each functional area follows the same pattern: one abstraction package + one or more provider packages.

## Banned Packages

**Do NOT use or reference these packages anywhere in the solution:**

| Package | Reason | Replacement |
|---------|--------|-------------|
| `AutoMapper` | Commercial license (v13+) | Manual mapping — extension method per DTO, or EF projection |
| `Mapster` | Hidden cost + debug pain — Phase 0 cleanup | Manual mapping — extension method per DTO, or EF projection |
| `FluentAssertions` | Commercial license (v7+) | `xunit` built-in `Assert.*` only |

If you see any banned package in a `.csproj` or `Directory.Packages.props`, remove it.

## Key Patterns

**Result pattern** (`Jaina.Core/Results/`): Use `Result` / `Result<T>` as return types instead of throwing exceptions for expected failures. Factory methods: `Result.Ok()`, `Result.Fail("msg")`. `Jaina.Core` is intentionally tiny — only the Result kernel; no Guard, no extensions. Argument validation: use `ArgumentNullException.ThrowIfNull(x)` from BCL.

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
- Target frameworks: `net8.0;net9.0;net10.0` (multi-target via `LibTfms` / `AppTfms` in `Directory.Build.props`).

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
    var result = Result.Ok(input);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal("hello", result.Value);
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
