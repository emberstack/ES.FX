# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Documentation (read first)

The authoritative, maintained documentation lives in **[`docs/`](docs/index.md)** — portable, SDK-style
Markdown. Use it as the source of truth for architecture and usage instead of duplicating it here:

- **Getting started / quickstart** → [`docs/getting-started/`](docs/getting-started/index.md)
- **Patterns & contributor rules** (Result/Problem, primitives, hosting, conventions, testing, *creating a
  library*) → [`docs/development/`](docs/development/index.md)
- **Additions** (one-per-dependency helpers) → [`docs/additions/`](docs/additions/index.md)
- **Ignite & Sparks** (the bootstrap, per-Spark reference, *creating a Spark*) → [`docs/ignite/`](docs/ignite/index.md)
- **Feature libraries** (Transactional Outbox, Migrations) → [`docs/libraries/`](docs/libraries/index.md)

> **Keep the docs in sync.** `docs/` is a first-class deliverable, not an afterthought. When you add,
> change, or remove a public API, package, Spark, or convention, update the matching `docs/` page **in the
> same change**. Treat documentation drift as a bug. Prefer linking to a docs page over repeating its
> content in this file or in code comments.

## Project Overview

ES.FX (EmberStack Framework) is a collection of reusable .NET extensions and application frameworks published
as NuGet packages under the `ES.FX.*` namespace. Every project in `src/` builds a package; `tests/` and
`playground/` are not published. The flagship is **Ignite**, an opinionated, "just add water" application
bootstrap (OpenTelemetry, health checks, resilience, service integrations).

All library projects target **.NET 10** (`net10.0`). Some stale `bin/obj` output may still reference
`net9.0` — trust the `.csproj`, not build artifacts.

## Commands

The solution uses the newer XML-based `.slnx` format (`ES.FX.slnx`) — there is no `.sln`. Recent `dotnet`
SDKs pick it up automatically at the repo root.

```bash
# Build (packages for ES.FX.* are produced automatically into .artifacts/nuget)
dotnet build
dotnet build --configuration Release

# Test — functional tests need Docker running (Testcontainers spins up real services)
dotnet test --verbosity normal
dotnet test tests/ES.FX.Tests/ES.FX.Tests.csproj              # one project
dotnet test --filter "FullyQualifiedName~ClassName.MethodName" # one test / class
dotnet test --logger "console;verbosity=detailed"

dotnet format          # apply formatting/style fixes
dotnet clean
```

Test results are written as TRX to `.artifacts/TestResults/`; NuGet packages to `.artifacts/nuget/`.

### Running the playground
```bash
dotnet run --project playground/Playground.Microservice.Api.Host      # ASP.NET API host
dotnet run --project playground/Playground.Microservice.Worker.Host    # background worker host
dotnet run --project playground/Playground.SimpleConsole               # minimal console
```

## Architecture (orientation — see `docs/` for depth)

Five independently consumable layers; dependencies point downward only:

1. **ES.FX** — framework-agnostic core primitives (`Result`/`Problem`, `Optional<T>`, `DurationValue`,
   `ValueRange`, BCL-style extensions). → [`docs/development/`](docs/development/index.md)
2. **ES.FX.Additions.\*** — focused, low-opinion helpers; each augments exactly one third-party dependency.
   → [`docs/additions/`](docs/additions/index.md)
3. **ES.FX.Hosting** — `ProgramEntry`/`ProgramEntryBuilder` wrap `Main` with structured startup, error
   handling, and graceful shutdown. → [`docs/development/hosting.md`](docs/development/hosting.md)
4. **ES.FX.Ignite** (+ `ES.FX.Ignite.Spark` base + the `ES.FX.Ignite.{Provider}` **Sparks**) — the
   opinionated bootstrap. → [`docs/ignite/`](docs/ignite/index.md)
5. **Feature libraries** — Transactional Outbox and Migrations, usable without Ignite. →
   [`docs/libraries/`](docs/libraries/index.md)

Ignite activates in **two phases**: `builder.Ignite(...)` on `IHostApplicationBuilder` (pre-build), then
`app.Ignite()` on `IHost` (post-build). A **Spark** plugs a service into Ignite (config binding, DI
registration, health checks, OpenTelemetry) and follows a fixed shape — study
`src/ES.FX.Ignite.StackExchange.Redis/` as the canonical example. Full model and per-Spark reference:
[`docs/ignite/`](docs/ignite/index.md) and [`docs/ignite/creating-a-spark.md`](docs/ignite/creating-a-spark.md).

## Conventions & Build Configuration

Global settings live in `Directory.Build.props` and apply to every project:
- **Warnings are errors** (`TreatWarningsAsErrors=true`), nullable + implicit usings enabled, XML docs
  generated, debug symbols embedded.
- `ES.FX.*` non-test projects **auto-pack on build** (`GeneratePackageOnBuild=true`) into `.artifacts/nuget`;
  they embed `README.md` and `package.icon.png`, MIT-licensed, `JetBrains.Annotations` referenced privately.
- Test projects (`*.Tests`) are excluded from packing and from code coverage, and emit per-project TRX loggers.
- **Central Package Management**: all versions are pinned in `Directory.Packages.props`
  (`ManagePackageVersionsCentrally=true`). Add/bump versions there, never inline in a `.csproj`.

Naming:
- Namespaces mirror folders: `ES.FX.{Component}.{SubComponent}`.
- Sparks: package `ES.FX.Ignite.{Provider}`, classes `{Service}Spark` / `{Service}SparkOptions` /
  `{Service}SparkSettings` / `{Service}HostingExtensions`. See
  [`docs/ignite/creating-a-spark.md`](docs/ignite/creating-a-spark.md).
- Tests: `{Project}.Tests`; integration hosts under test are `{Project}.Tests.SUT`; fixtures are
  `{Service}Fixture`.

## Testing

- **xUnit v3** (`xunit.v3`), Moq, coverlet for coverage.
- Functional tests use **Testcontainers** and require a running Docker engine: MsSql, Redis, PostgreSQL,
  MariaDB. Shared fixtures live in `tests/ES.FX.Shared.{Db}.Tests`.
- `.SUT` projects are real hosts (ASP.NET etc.) started via `Microsoft.AspNetCore.Mvc.Testing` for
  end-to-end coverage of a Spark. Details: [`docs/development/testing.md`](docs/development/testing.md).

## CI/CD

`.github/workflows/pipeline.yaml` (single pipeline, all branches):
- Installs .NET `10.x` and **GitVersion 6.x**; `dorny/paths-filter` skips build when only non-source paths change.
- Versioning via `GitVersion.yaml` (`ContinuousDelivery` mode, commit-message bumps like `+semver: minor`, tag prefix `v`).
- `main` builds `Release` and **publishes to GitHub Packages + NuGet.org** and cuts a GitHub release; other
  branches build `Debug` and don't publish. Dependabot PRs don't push packages.
- Dependency bumps are grouped via `.github/dependabot.auto.yaml`; stale issues handled by `stale.yaml`.
