# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Restore and build
dotnet restore Jaina.sln
dotnet build Jaina.sln
dotnet build Jaina.sln -c Release

# Run a specific sample
dotnet run --project samples/Jaina.Samples.WebApi

# Run tests (once test projects exist)
dotnet test Jaina.sln
dotnet test tests/Jaina.<Module>.Tests/<project>.csproj  # single project
dotnet test --filter "FullyQualifiedName~ClassName"      # single test class
```

## Architecture

Jaina is a modular .NET 8 framework library organized into independent packages:

```
src/
  core/         Jaina.Core            — Guard, Result<T>, extensions, HttpClientBase
  caching/      Jaina.Caching*        — ICache abstraction + Memory/Redis/Fusion impls
  data/         Jaina.Data            — Repository, Unit of Work (EF Core + Dapper)
                Jaina.Data.Cqrs       — Command/Query buses, domain events, event store
  messaging/    Jaina.Messaging*      — IQueue<T>/ITopic<T> + RabbitMQ/ServiceBus/Broadcast
  storage/      Jaina.Storage*        — IFileStorage + Local/AzureBlob/FileShare/SFTP
  security/     Jaina.Security        — AES/RSA/BCrypt/JWT
                Jaina.Security.Authentication* — JWT bearer auth, Azure KeyVault
  diagnostics/  Jaina.Diagnostics*    — ITelemetry + AppInsights/ElasticAPM/NLog
  notifications/ Jaina.Notifications  — IEmailSender (SMTP), ISmsSender
samples/        Aspire AppHost, WebApi, Worker demos
tests/          (empty — test infra configured, no projects yet)
```

Each functional area follows the same pattern: one abstraction package + one or more provider packages.

## Key Patterns

**Result pattern** (`Jaina.Core/Results/`): Use `Result` / `Result<T>` as return types instead of throwing exceptions for expected failures. Factory methods: `Result.Ok()`, `Result.Fail("msg")`.

**Guard clauses** (`Jaina.Core/Guard.cs`): Validate arguments at entry points. Methods: `Guard.NotNull()`, `Guard.NotNullOrEmpty()`, `Guard.Requires<TException>()`. Uses `CallerArgumentExpression` — no need to pass parameter name strings.

**DI registration**: Every module exposes `AddJaina<Feature>()` extension methods on `IServiceCollection`. Use `TryAddSingleton`/`TryAddScoped` so callers can override implementations.

**Repository / Unit of Work**: `IRepository<TEntity>` and `IUnitOfWork` in `Jaina.Data`. EF Core implementations in `EfRepository` / `EfUnitOfWork`. Raw SQL via `DapperRepository`.

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
- Target frameworks: `netstandard2.0;net8.0` for pure libs; `net8.0` only when using ASP.NET Core or EF Core

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
