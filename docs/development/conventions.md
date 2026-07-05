---
title: Conventions and build configuration
description: The MSBuild, packaging, versioning, and CI conventions every ES.FX package follows, centralized in Directory.Build.props and Directory.Packages.props.
---

Every project in the ES.FX repository inherits a shared set of build, packaging, and quality rules. You do not configure them per project — they live in two repo-root MSBuild files (`Directory.Build.props`, `Directory.Packages.props`) that MSBuild imports automatically for every `.csproj` beneath them. This page documents those conventions so you know what a new `ES.FX.*` project gets for free and which rules you must respect.

> [!NOTE]
> This page describes the repository's own build system — it is for contributors working **on** ES.FX. If you only consume the packages, see [Installation](../getting-started/installation.md).

## Solution layout

The solution uses the XML-based `.slnx` format (`ES.FX.slnx`) at the repo root — there is no `.sln`. Recent `dotnet` SDKs pick it up automatically, so `dotnet build` / `dotnet test` from the root just work.

The tree is organized by purpose:

| Path | Contents | Packs? |
| --- | --- | --- |
| `src/` | The libraries. Every project here builds an `ES.FX.*` NuGet package. | Yes |
| `tests/` | xUnit test projects (`*.Tests`) and system-under-test hosts (`*.Tests.SUT`). | No |
| `playground/` | Runnable sample hosts (`Playground.*`) that exercise the framework end to end. | No |

Projects are grouped into solution folders (`.src/FX`, `.src/FX/Additions`, `.src/Ignite`, `.src/Ignite/Sparks`, `tests/…`, `playground/…`) inside `ES.FX.slnx`. When you add a project, add its `<Project Path="…" />` entry to the matching folder.

## Central Package Management

All NuGet versions are pinned centrally in `Directory.Packages.props` — `ManagePackageVersionsCentrally` is `true`. Individual `.csproj` files reference packages **without** a `Version` attribute:

```xml
<!-- In a project .csproj -->
<ItemGroup>
  <PackageReference Include="StackExchange.Redis" />
</ItemGroup>
```

The version comes from the central file:

```xml
<!-- In Directory.Packages.props -->
<PackageVersion Include="StackExchange.Redis" Version="3.0.11" />
```

> [!IMPORTANT]
> Never put a `Version` on a `<PackageReference>` inside a project — with Central Package Management on, that is an error. To add or bump a dependency, edit the `<PackageVersion>` entry in `Directory.Packages.props`. Dependabot bumps land there too.

`CentralPackageTransitivePinningEnabled` is `false`, so only your direct references are pinned; transitive versions are resolved normally by NuGet.

## Global build settings

`Directory.Build.props` applies these `PropertyGroup` defaults to **every** project in the repo:

| Setting | Value | Effect |
| --- | --- | --- |
| `TreatWarningsAsErrors` | `true` | A warning fails the build. Fix warnings; do not suppress broadly. |
| `Nullable` | `enable` | Nullable reference types are on everywhere. |
| `ImplicitUsings` | `enable` | Common `using`s are implicit. |
| `GenerateDocumentationFile` | `true` | XML docs are produced from `///` comments. |
| `DebugType` | `embedded` | Debug symbols are embedded in the assembly. |

A curated `NoWarn` list suppresses the noisier XML-doc diagnostics (`CS1591` and friends) and the NuGet audit warnings (`NU1901`–`NU1903`) so that missing doc comments and advisories do not break the warnings-as-errors build. `JetBrains.Annotations` is referenced by every project with `PrivateAssets="All"` (compile-time only, never flowed to consumers).

> [!WARNING]
> Because warnings are errors, an unused variable, an unhandled nullable case, or an obsolete API will stop the build. Run `dotnet build` before pushing — CI enforces the same rule.

## Packaging conventions

Any project whose name starts with `ES.FX` and does **not** contain `.Tests` is a package project. `Directory.Build.props` turns these into packages automatically:

- `GeneratePackageOnBuild` is `true` — building the project also produces the `.nupkg`. No separate `dotnet pack` step is required.
- Packages are written to `.artifacts/nuget` (at the repo root, via `PackageOutputPath`).
- Every package embeds the repo-root `README.md` (`PackageReadmeFile`) and `package.icon.png` (`PackageIcon`), is `Authors=emberstack` / `Company=EmberStack`, MIT-licensed (`PackageLicenseExpression=MIT`), and points `RepositoryUrl` at the GitHub repo.

Test projects (`*.Tests`) opt out: `GeneratePackageOnBuild` is forced back to `false`, they are decorated with `[ExcludeFromCodeCoverage]`, and they emit a per-project TRX logger to `.artifacts/TestResults`.

> [!NOTE]
> The `README.md` and `package.icon.png` shipped in each package are the **repo-root** files, shared by every package. There is no per-project README in the `.nupkg`.

## Naming conventions

Namespaces mirror folder structure: a type under `src/ES.FX.Ignite.StackExchange.Redis/Hosting/` lives in `ES.FX.Ignite.StackExchange.Redis.Hosting`. Package identity follows the layer:

| Layer | Package name | Type naming |
| --- | --- | --- |
| Core | `ES.FX` | BCL-style folders (`Collections`, `Results`, `Problems`, `Primitives`, …). |
| Addition | `ES.FX.Additions.{Library}` | Augments exactly one third-party library. |
| Spark | `ES.FX.Ignite.{Provider}` | `{Service}Spark`, `{Service}SparkOptions`, `{Service}SparkSettings`, `{Service}HostingExtensions` (entry points named `Ignite{Service}…`). |
| Tests | `{Project}.Tests` | System-under-test hosts are `{Project}.Tests.SUT`; fixtures are `{Service}Fixture`. |

For the full Spark shape and the Settings-vs-Options split, see [Creating a Spark](../ignite/creating-a-spark.md). For scaffolding a whole new package, see [Creating a new ES.FX library](./creating-a-library.md).

## Versioning

Versions are computed by **GitVersion 6.x** from `GitVersion.yaml`, not hand-set in any `.csproj`:

- Mode is `ContinuousDelivery`; the tag prefix is `v` (matched as `[vV]`).
- The version bumps from **commit-message trailers**: `+semver: major` / `+semver: minor` / `+semver: patch` (aliases `breaking`/`feature`/`fix` also work), and `+semver: none` to skip a bump.
- Branch labels differentiate pre-release builds (`develop`, `rc` on `release/*`, per-branch labels on `feature/*`, etc.). `main` produces the clean release version.

CI passes the computed version into the build as `/p:Version=…`, so the number stamped on the assembly and the package comes from Git history — you do not edit version numbers by hand.

## Continuous integration

A single pipeline, `.github/workflows/pipeline.yaml`, runs on every branch and pull request:

1. **Discovery** installs .NET `10.x` and GitVersion `6.x`, computes the version, and uses `dorny/paths-filter` to decide what to run. The source filter (`src`, which also gates package pushes) matches `src/**`, `*.slnx`, `*.sln`, and `*.props`; the build filter adds `tests/**` and `playground/**`.
2. **Build** runs `dotnet restore` → `dotnet build` → `dotnet test`, publishes the TRX test report, and uploads the `.nupkg` artifacts. Configuration is `Release` on `main`, `Debug` elsewhere. On non-pull-request, non-Dependabot pushes that touched source, this job also pushes the packages to **GitHub Packages**.
3. **Release** runs only on `main`: it pushes packages to **GitHub Packages** and **NuGet.org** and cuts a GitHub release named/tagged `v{version}`.

> [!IMPORTANT]
> Package publishing is gated. Pull requests and Dependabot-authored runs never push packages. Pushes to NuGet.org and the GitHub release happen only on `main`; other branches may still push to GitHub Packages when source changes.

## See also

- [Creating a new ES.FX library](./creating-a-library.md) — the step-by-step checklist for adding a package that fits these conventions.
- [Application hosting](./hosting.md) — the `ProgramEntry` / `ProgramEntryBuilder` lifecycle wrapper (note: `UseSerilog()` comes from the [Serilog addition](../additions/serilog.md), not Hosting).
- [Testing](./testing.md) — xUnit v3, Testcontainers, and the TRX output layout referenced above.
- [Installation](../getting-started/installation.md) — consuming the published packages, from a consumer's point of view.
- [Development](./index.md) — the contributor landing page.
