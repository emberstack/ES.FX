---
title: Creating a new ES.FX library
description: A concrete checklist for scaffolding a new ES.FX.* NuGet package that fits the repository conventions and auto-packs on build.
---

Adding a new package to ES.FX is mostly about *not* re-declaring what the repo already declares for you.
`Directory.Build.props` and `Directory.Packages.props` handle packaging, framework targeting, warnings,
XML docs, and version pinning centrally — a new project `.csproj` stays tiny. This page is the rulebook:
what to name things, where to put them, and what you get for free.

> [!NOTE]
> This page is about authoring a package that ships from the ES.FX repo itself. For how the repo builds,
> tests, and versions as a whole, see [Development](./index.md) and
> [Conventions and build config](./conventions.md).

## What you get for free

Every project whose name starts with `ES.FX` inherits these from `Directory.Build.props` at the repo root —
you do **not** repeat them in your `.csproj`:

| Setting | Value | Effect |
| --- | --- | --- |
| `GeneratePackageOnBuild` | `true` | The project **packs to a `.nupkg` on every build** into `.artifacts/nuget`. |
| `TreatWarningsAsErrors` | `true` | A warning fails the build. |
| `Nullable` / `ImplicitUsings` | `enable` | Nullable reference types and implicit `global using`s are on. |
| `GenerateDocumentationFile` | `true` | XML docs are produced; missing-doc warnings (`CS1591`, etc.) are suppressed but you should still document public APIs. |
| `DebugType` | `embedded` | Debug symbols are embedded in the assembly. |
| Package metadata | `Authors`, `Company`, `RepositoryUrl`, `MIT` license, icon, README | Applied to the produced package automatically. |
| `JetBrains.Annotations` | referenced with `PrivateAssets="All"` | Available for annotations, not exposed to consumers. |
| `README.md` + `package.icon.png` | packed from the repo root | Embedded in the `.nupkg`. |

> [!IMPORTANT]
> The repo uses **Central Package Management** (`ManagePackageVersionsCentrally=true` in
> `Directory.Packages.props`). A `<PackageReference>` inside the repo carries **no `Version` attribute** —
> the version is pinned centrally. See [Reference packages](#reference-packages) below.

## Checklist

1. Pick the layer and name (see [Naming rules](#naming-rules)).
2. Create `src/{PackageName}/{PackageName}.csproj` with the [minimal project shape](#minimal-project-shape).
3. Add references — in-repo `ProjectReference`s and third-party `PackageReference`s **without versions**.
4. Pin any new third-party version in `Directory.Packages.props` (never inline).
5. Register the project in `ES.FX.slnx`.
6. Add a matching test project `tests/{PackageName}.Tests/` (see [Add a test project](#add-a-test-project)).
7. Build — the package auto-packs to `.artifacts/nuget`.

## Naming rules

Namespaces mirror folders: a type in `src/ES.FX.Foo.Bar/Baz/` lives in namespace `ES.FX.Foo.Bar.Baz`.
The package id equals the project (and root folder) name. Choose the prefix by layer:

| Layer | Package id form | Example | Notes |
| --- | --- | --- | --- |
| Core primitives | `ES.FX` | `ES.FX` | The single framework-agnostic core package. |
| Addition | `ES.FX.Additions.{Dependency}` | `ES.FX.Additions.FluentValidation` | Augments **exactly one** third-party dependency; the id names it. |
| Hosting | `ES.FX.Hosting` | `ES.FX.Hosting` | The lifecycle wrapper. |
| Feature library | `ES.FX.{Feature}` | `ES.FX.TransactionalOutbox` | Standalone, independent of Ignite. |
| Ignite Spark | `ES.FX.Ignite.{Provider}` | `ES.FX.Ignite.StackExchange.Redis` | One integration per package; follows the fixed Spark shape. |

If you are building a Spark, the folder layout and class naming
(`{Service}Spark`, `{Service}SparkOptions`, `{Service}SparkSettings`, `{Service}HostingExtensions`) are a
separate, stricter contract — see [Creating a Spark](../ignite/creating-a-spark.md).

## Minimal project shape

A new library `.csproj` only declares what is *not* inherited: the SDK, the target framework, and its
references. Model it on the canonical Redis Spark project (`src/ES.FX.Ignite.StackExchange.Redis`):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="StackExchange.Redis" />
    <ProjectReference Include="..\ES.FX.Ignite.Spark\ES.FX.Ignite.Spark.csproj" />
  </ItemGroup>

</Project>
```

> [!NOTE]
> All ES.FX libraries target **`net10.0`**. Some stale `bin`/`obj` output may still show `net9.0` — trust
> the `.csproj`, not build artifacts. Do **not** add `<GeneratePackageOnBuild>`, `<Authors>`,
> `<PackageLicenseExpression>`, or an `<IsPackable>` — those come from `Directory.Build.props`.

## Reference packages

Third-party references have **no `Version`**. The version is pinned once, centrally, in
`Directory.Packages.props`:

```xml
<PackageReference Include="StackExchange.Redis" />
```

If the package version is not yet listed in `Directory.Packages.props`, add a `<PackageVersion>` entry there:

```xml
<ItemGroup>
  <PackageVersion Include="StackExchange.Redis" Version="3.0.11" />
</ItemGroup>
```

> [!WARNING]
> Never put a `Version` attribute on a `<PackageReference>` inside the repo. With Central Package
> Management enabled, an inline version is an error. Bump or add versions only in
> `Directory.Packages.props`.

In-repo dependencies use `<ProjectReference>` and point at the target `.csproj` relative to your project.
Keep the dependency direction pointing **downward** — a lower layer must never reference a higher one (core
depends on nothing in-repo; Additions depend only on core; Sparks depend on `ES.FX.Ignite.Spark` and their
own Addition/third-party package).

## Register in the solution

The solution is the XML-based `ES.FX.slnx` (there is no `.sln`). Add your project under the appropriate
folder node:

```xml
<Project Path="src/ES.FX.MyFeature/ES.FX.MyFeature.csproj" />
```

## Add a test project

Every library gets a sibling test project named `{PackageName}.Tests` under `tests/`. Test projects are
**excluded from packing and code coverage** automatically (the `.Tests` suffix is what triggers that in
`Directory.Build.props`), and each emits a per-project TRX logger to `.artifacts/TestResults/`.

The stack is **xUnit v3** with Moq, coverlet for coverage, and Testcontainers for functional tests that need
a real service. Model the test `.csproj` on the Redis Spark's tests:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\ES.FX.MyFeature\ES.FX.MyFeature.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

Naming conventions for test assets:

- Unit/functional tests: `{Project}.Tests`.
- A real host started under test (ASP.NET etc.): `{Project}.Tests.SUT`.
- A shared Testcontainers fixture: `{Service}Fixture`, kept in `tests/ES.FX.Shared.{Db}.Tests`.

> [!IMPORTANT]
> Functional tests that use Testcontainers require a running **Docker** engine — the tests spin up real
> services (MsSql, Redis, PostgreSQL, MariaDB). See [Testing](./testing.md) for the full workflow.

Register the test project in `ES.FX.slnx` under the tests folder just as you did the library.

## Build and verify

```bash
dotnet build
```

Confirm the package landed in `.artifacts/nuget`:

```bash
ls .artifacts/nuget
```

Then run the tests:

```bash
dotnet test tests/ES.FX.MyFeature.Tests/ES.FX.MyFeature.Tests.csproj --verbosity normal
```

Apply formatting before you commit:

```bash
dotnet format
```

## See also

- [Development](./index.md) — building, testing, and running the repo.
- [Conventions and build config](./conventions.md) — the global build properties and versioning in depth.
- [Testing](./testing.md) — xUnit v3, Moq, and Testcontainers.
- [Creating a Spark](../ignite/creating-a-spark.md) — the stricter contract for an Ignite integration package.
- [Application hosting](./hosting.md) — the `ProgramEntry` lifecycle wrapper.
