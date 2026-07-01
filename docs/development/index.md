---
title: Development
description: How to build, test, and contribute to the ES.FX repository, and the conventions every ES.FX.* package follows.
---

This is the contributor-facing guide for working **on** ES.FX itself — building the solution, running the
tests, exercising the playground hosts, and following the conventions every package shares. It is distinct
from [Getting started](../getting-started/index.md), which is about *consuming* ES.FX in your own app.

The base `ES.FX` package also ships the framework-agnostic primitives (`Result` / `Problem`, `Optional<T>`,
BCL-style extensions). Those have their own pages under this section — see
[Results and problems](./results-and-problems.md), [Primitives](./primitives.md), and
[Core extensions](./core-extensions.md).

## Solution layout

The repository uses the newer XML-based solution format, `ES.FX.slnx` — there is **no** `.sln`. Recent
`dotnet` SDKs pick it up automatically at the repo root.

| Folder | Contents | Packed? |
| --- | --- | --- |
| `src/` | Every publishable library. Each project builds one `ES.FX.*` NuGet package. | Yes |
| `tests/` | xUnit v3 test projects (`*.Tests`) and system-under-test hosts (`*.Tests.SUT`). | No |
| `playground/` | Runnable sample hosts (API, worker, console) that exercise the framework. | No |

Two files at the repo root drive every project:

- `Directory.Build.props` — global MSBuild defaults applied to every project (see [Conventions](#conventions)).
- `Directory.Packages.props` — **Central Package Management**: every NuGet version is pinned here, never
  inline in a `.csproj`.

## Build and pack

Building any `ES.FX.*` (non-test) project **automatically produces its NuGet package** — `GeneratePackageOnBuild`
is on for those projects. Packages land in `.artifacts/nuget`.

```bash
# Debug build — packs ES.FX.* into .artifacts/nuget
dotnet build

# Release build — what CI publishes from main
dotnet build --configuration Release
```

```bash
# Clean build outputs
dotnet clean
```

> [!NOTE]
> Only projects whose name starts with `ES.FX` (and does not contain `.Tests`) pack. Test and playground
> projects are excluded from packing.

> [!TIP]
> Some stale `bin/obj` output may still reference `net9.0`. Trust the `.csproj` (`net10.0`), not build
> artifacts.

## Test

Tests run with **xUnit v3**, Moq, and coverlet for coverage.

```bash
# Run the whole suite
dotnet test --verbosity normal

# One project
dotnet test tests/ES.FX.Tests/ES.FX.Tests.csproj

# One test or class by name
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"

# Detailed console output
dotnet test --logger "console;verbosity=detailed"
```

Test results are written as TRX to `.artifacts/TestResults/`.

> [!IMPORTANT]
> Functional tests use **Testcontainers** and require a running **Docker** engine — they spin up real
> services (MsSql, Redis, PostgreSQL, MariaDB) in containers. Without Docker, those tests fail to start.

See [Testing](./testing.md) for the fixture model, `.SUT` hosts, and coverage details.

## Run the playground

Three runnable hosts under `playground/` demonstrate the framework end to end. Each calls
`ProgramEntry.CreateBuilder(args)` and activates Ignite (see [Application hosting](./hosting.md) and the
[Ignite overview](../ignite/index.md)).

```bash
# ASP.NET Core API host — the full web pipeline, Sparks, health endpoints, OpenAPI
dotnet run --project playground/Playground.Microservice.Api.Host

# Background worker host — non-web host; Ignite with no web middleware
dotnet run --project playground/Playground.Microservice.Worker.Host

# Minimal console — the smallest ProgramEntry wrapper
dotnet run --project playground/Playground.SimpleConsole
```

## Conventions

`Directory.Build.props` applies these to every project — you do not repeat them per `.csproj`:

- **Warnings are errors** (`TreatWarningsAsErrors=true`).
- **Nullable reference types** and **implicit usings** enabled.
- **XML documentation** generated (`GenerateDocumentationFile=true`).
- **Debug symbols embedded** (`DebugType=embedded`).
- Packable `ES.FX.*` projects embed `README.md` and `package.icon.png`, are **MIT**-licensed, and
  reference `JetBrains.Annotations` privately.

Central Package Management is mandatory: add or bump a version in `Directory.Packages.props`, and reference
it from a `.csproj` **without** a `Version` attribute.

```xml
<!-- In a project .csproj — no Version attribute -->
<ItemGroup>
  <PackageReference Include="StackExchange.Redis" />
</ItemGroup>
```

```xml
<!-- In Directory.Packages.props — the single source of versions -->
<ItemGroup>
  <PackageVersion Include="StackExchange.Redis" Version="2.8.16" />
</ItemGroup>
```

Naming mirrors folders: namespaces are `ES.FX.{Component}.{SubComponent}`, Additions are
`ES.FX.Additions.{Library}`, and Sparks are `ES.FX.Ignite.{Provider}` with classes `{Service}Spark` /
`{Service}SparkOptions` / `{Service}SparkSettings` / `{Service}HostingExtensions`. See
[Conventions and build config](./conventions.md) for the full rules and CI/versioning, and
[Creating a new ES.FX library](./creating-a-library.md) for the step-by-step checklist to add a package.

## See also

- [Creating a new ES.FX library](./creating-a-library.md) — scaffold a package that fits the conventions.
- [Application hosting](./hosting.md) — the `ProgramEntry` lifecycle wrapper.
- [Conventions and build config](./conventions.md) — `Directory.Build.props`, CPM, CI, versioning.
- [Testing](./testing.md) — xUnit v3, Testcontainers, and the `.SUT` hosts.
- [Results and problems](./results-and-problems.md) and [Primitives](./primitives.md) — the core `ES.FX` types.
