---
title: Installation
description: Install the ES.FX NuGet packages, understand package naming and Central Package Management, and stand up a working Ignite host.
---

ES.FX ships as a set of independently consumable NuGet packages under the `ES.FX.*` prefix. Take only the layer you need: the framework-agnostic core, a focused Addition, the Hosting lifecycle wrapper, a standalone feature library, or the full [Ignite](../ignite/index.md) bootstrap with one or more [Sparks](../ignite/sparks/index.md). This page covers where the packages come from, how to install them, and a minimal end-to-end Ignite host you can copy and run.

## Prerequisites

- **.NET 10 SDK** or newer. Every ES.FX library targets `net10.0`.
- An IDE or editor (Visual Studio, Rider, or VS Code with the C# Dev Kit).
- **Docker** is only needed if you run the ES.FX functional test suite (Testcontainers spins up real services). It is not required to consume the packages. See [Testing](../development/testing.md).

## Where the packages come from

ES.FX packages are published to two feeds:

- **NuGet.org** — the primary public feed. Nothing extra to configure; the default `nuget.org` source resolves `ES.FX.*` out of the box.
- **GitHub Packages** — the same releases are also pushed to the `emberstack` GitHub Packages registry.

For almost every consumer, the default NuGet.org source is all you need.

> [!NOTE]
> Only builds from `main` publish releases. The public `ES.FX.*` versions you install from NuGet.org are cut from `main` (versioned with GitVersion, tag prefix `v`).

## Package naming

Package names mirror the layer and the thing they wrap, so you can predict them:

| Layer | Package pattern | Example |
| --- | --- | --- |
| Core primitives | `ES.FX` | `ES.FX` |
| Addition (augments one third-party library) | `ES.FX.Additions.{Library}` | `ES.FX.Additions.FluentValidation` |
| Hosting lifecycle | `ES.FX.Hosting` | `ES.FX.Hosting` |
| Ignite bootstrap | `ES.FX.Ignite` | `ES.FX.Ignite` |
| Spark (one integration for Ignite) | `ES.FX.Ignite.{Provider}` | `ES.FX.Ignite.StackExchange.Redis` |
| Standalone feature library | `ES.FX.{Feature}` | `ES.FX.TransactionalOutbox` |

Browse the full set on the [Sparks catalog](../ignite/sparks/index.md) and the [Additions catalog](../additions/index.md).

## Install the packages

Install with the .NET CLI:

```bash
dotnet add package ES.FX.Hosting
dotnet add package ES.FX.Ignite
dotnet add package ES.FX.Additions.Serilog
dotnet add package ES.FX.Ignite.Serilog
dotnet add package ES.FX.Ignite.StackExchange.Redis
```

Or add `<PackageReference>` items to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="ES.FX.Hosting" Version="1.0.0" />
  <PackageReference Include="ES.FX.Ignite" Version="1.0.0" />
  <PackageReference Include="ES.FX.Additions.Serilog" Version="1.0.0" />
  <PackageReference Include="ES.FX.Ignite.Serilog" Version="1.0.0" />
  <PackageReference Include="ES.FX.Ignite.StackExchange.Redis" Version="1.0.0" />
</ItemGroup>
```

> [!TIP]
> Replace the `Version` values with the latest published versions from NuGet.org.

### Central Package Management

If your solution uses **Central Package Management** (CPM) — pinning every version in `Directory.Packages.props` — declare each version once there and omit the `Version` attribute on the `<PackageReference>`. ES.FX itself is built this way.

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="ES.FX.Hosting" Version="1.0.0" />
    <PackageVersion Include="ES.FX.Ignite" Version="1.0.0" />
    <PackageVersion Include="ES.FX.Additions.Serilog" Version="1.0.0" />
    <PackageVersion Include="ES.FX.Ignite.Serilog" Version="1.0.0" />
    <PackageVersion Include="ES.FX.Ignite.StackExchange.Redis" Version="1.0.0" />
  </ItemGroup>
</Project>
```

```xml
<!-- Your .csproj — no Version attribute under CPM -->
<ItemGroup>
  <PackageReference Include="ES.FX.Hosting" />
  <PackageReference Include="ES.FX.Ignite" />
  <PackageReference Include="ES.FX.Additions.Serilog" />
  <PackageReference Include="ES.FX.Ignite.Serilog" />
  <PackageReference Include="ES.FX.Ignite.StackExchange.Redis" />
</ItemGroup>
```

See [Conventions & build config](../development/conventions.md) for how ES.FX applies CPM across the repository.

## Bootstrap with Ignite

Ignite is a **two-phase** bootstrap, and the recommended entry point wraps it in [`ProgramEntry`](../development/hosting.md) for structured startup, logging, and graceful shutdown:

1. `ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(...)` wraps `Main` with lifecycle handling. `UseSerilog()` comes from `ES.FX.Additions.Serilog`.
2. Inside the callback, create the host builder and call `builder.Ignite(...)` — **phase A**, on `IHostApplicationBuilder`, before `Build()`.
3. Register any Sparks (for example `builder.IgniteRedisClient()`) between `builder.Ignite(...)` and `builder.Build()`.
4. After `Build()`, call `app.Ignite()` — **phase B**, on the built `IHost`. For a `WebApplication` this also wires middleware and health-check endpoints.

> [!IMPORTANT]
> `builder.Ignite(...)` and `app.Ignite()` are distinct extensions on distinct types. Phase A configures the builder; phase B finalizes the built host. Web-only middleware in phase B runs only when the host is a `WebApplication`.

The following `Program.cs` is a complete, runnable API host that mirrors the ES.FX playground composition:

```csharp
using ES.FX.Additions.Serilog.Lifetime;
using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Ignite.StackExchange.Redis.Hosting;
using StackExchange.Redis;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);

    // Route logging through Serilog.
    builder.Logging.ClearProviders();
    builder.IgniteSerilog();

    // Phase A: activate Ignite on the host builder.
    builder.Ignite(settings =>
    {
        settings.AspNetCore.JsonStringEnumConverterEnabled = true;
    });

    // Add a Spark: registers a shared IConnectionMultiplexer with health checks and tracing.
    builder.IgniteRedisClient();

    var app = builder.Build();

    // Phase B: finalize Ignite on the built host (middleware + health endpoints for WebApplication).
    app.Ignite();

    app.MapGet("/ping", async (IConnectionMultiplexer redis) =>
    {
        var pong = await redis.GetDatabase().PingAsync();
        return Results.Ok(new { RoundTrip = pong.ToString() });
    });

    await app.RunAsync();
    return 0;
});
```

> [!NOTE]
> A background worker or console host is identical except you call `Host.CreateApplicationBuilder(args)` instead of `WebApplication.CreateBuilder(args)`. Both call `builder.Ignite()` and `app.Ignite()`; non-web hosts simply get no web middleware.

## Configure the Ignite section

All ES.FX configuration lives under a rooted `Ignite:` section in `appsettings.json`. Ignite's own observability toggles bind under `Ignite:Settings`; each Spark reads its own sub-section (Redis reads `Ignite:Redis`). A minimal configuration for the host above:

```json
{
  "Ignite": {
    "Redis": {
      "ConnectionString": "localhost:6379"
    }
  }
}
```

> [!IMPORTANT]
> Mind the split: a Spark's **Options** (service config, such as `ConnectionString`) bind at `Ignite:Redis` directly, while its **Settings** (observability toggles) bind at `Ignite:Redis:Settings`. See the [Ignite configuration model](../ignite/configuration.md) for the full layout.

## Run it

Restore, build, and run:

```bash
dotnet run
```

With the Redis Spark registered and `app.Ignite()` called, the host gives you out of the box:

- A shared `IConnectionMultiplexer` in DI (injected into the `/ping` endpoint above).
- **Health-check endpoints** — readiness (all checks) and liveness (checks tagged `"live"` only). The Redis health check participates in readiness by default.
- **OpenTelemetry** logging, metrics, and tracing, with HttpClient/ASP.NET Core instrumentation and a tracing source for Redis.
- **HttpClient resilience** and standardized ProblemDetails error responses.

## Next steps

- [Quickstart](./quickstart.md) — build your first Ignite app step by step.
- [Core concepts](./concepts.md) — the Ignite two-phase model, Sparks, and Settings vs Options.
- [Ignite overview](../ignite/index.md) — everything the bootstrap wires for you.
- [Sparks catalog](../ignite/sparks/index.md) — plug in Redis, EF Core, Azure, and more.

## See also

- [Redis client integration](../ignite/sparks/stackexchange-redis.md) — the Spark used in this example.
- [Application hosting](../development/hosting.md) — `ProgramEntry` and the lifecycle wrapper.
- [Conventions & build config](../development/conventions.md) — Central Package Management in ES.FX.
- [Ignite configuration model](../ignite/configuration.md) — the `Ignite:` section and the `:Settings` sub-node.
