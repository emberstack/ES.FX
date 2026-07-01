---
title: Introduction
description: Get started with ES.FX and stand up your first Ignite-powered .NET 10 service end to end.
---

ES.FX is a collection of reusable .NET 10 libraries published as `ES.FX.*` NuGet packages. It is
organized into independently consumable layers — framework-agnostic **core primitives**, focused
**Additions** that each augment one third-party library, a **Hosting** lifecycle wrapper, standalone
**feature libraries**, and **Ignite**, the "just add water" application bootstrap that wires
OpenTelemetry, health checks, resilience, and service integrations for you.

This page takes you from an empty project to a running Ignite web service. If you just want the
vocabulary first, jump to [Core concepts](./concepts.md); if you want package-source details (NuGet.org
and GitHub Packages), see [Installation](./installation.md).

## Prerequisites

- The **.NET 10 SDK**. Every ES.FX library targets `net10.0`.
- An IDE or editor (Visual Studio, Rider, or VS Code with the C# Dev Kit).
- **Docker** is only needed to run the ES.FX repository's functional tests (they spin up real services
  with Testcontainers). You do **not** need Docker to consume ES.FX packages in your own app.

> [!NOTE]
> This guide targets a standalone consumer project, so the `<PackageReference>` snippets include a
> `Version` attribute. Inside the ES.FX repository itself, versions are centralized and the attribute is
> omitted — see [Conventions & build config](../development/conventions.md).

## Install the packages

Ignite lives in `ES.FX.Ignite`; the Serilog wiring used by the entry point comes from
`ES.FX.Additions.Serilog`. For the example below you also add the Redis Spark
(`ES.FX.Ignite.StackExchange.Redis`).

```bash
dotnet add package ES.FX.Ignite
dotnet add package ES.FX.Additions.Serilog
dotnet add package ES.FX.Ignite.StackExchange.Redis
```

```xml
<ItemGroup>
  <PackageReference Include="ES.FX.Ignite" Version="*" />
  <PackageReference Include="ES.FX.Additions.Serilog" Version="*" />
  <PackageReference Include="ES.FX.Ignite.StackExchange.Redis" Version="*" />
</ItemGroup>
```

## Bootstrap with Ignite

An ES.FX service is composed in three moves, using the same pattern as the playground hosts:

1. `ProgramEntry.CreateBuilder(args)` wraps your `Main` with structured startup, error handling, and
   graceful shutdown. `.UseSerilog()` (from `ES.FX.Additions.Serilog`) makes Serilog the bootstrap
   logger, and `.Build().RunAsync(...)` runs your composition inside that lifecycle.
2. Inside the callback you build a normal `WebApplication`, call `builder.Ignite(...)` to activate the
   framework **pre-build**, and add any Sparks.
3. After `builder.Build()`, call `app.Ignite()` to finalize middleware and health endpoints
   **post-build**, then `app.RunAsync()`.

> [!IMPORTANT]
> Ignite is two-phase. `builder.Ignite(...)` runs on the `IHostApplicationBuilder` **before** the host
> is built; `app.Ignite()` runs on the built `IHost` **after**. Web-only middleware (forwarded headers,
> exception handling, health endpoints) is wired by `app.Ignite()` only when the host is a
> `WebApplication`. See the [Ignite overview](../ignite/index.md) for the full model.

Create `Program.cs`:

```csharp
using ES.FX.Additions.Serilog.Lifetime;
using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.StackExchange.Redis.Hosting;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);

    // Activate Ignite (Phase A — pre-build).
    builder.Ignite(settings =>
    {
        settings.AspNetCore.JsonStringEnumConverterEnabled = true;
    });

    // Add a Spark: registers a shared IConnectionMultiplexer with health checks and tracing.
    builder.IgniteRedisClient();

    var app = builder.Build();

    // Finalize Ignite (Phase B — post-build): middleware + health endpoints.
    app.Ignite();

    app.MapGet("/", () => "It works!");

    await app.RunAsync();
    return 0;
});
```

## Add your first Spark

A **Spark** is a self-contained integration that plugs a service into Ignite: it binds configuration,
registers the service in DI, and wires a health check plus OpenTelemetry. The `builder.IgniteRedisClient()`
call above is the Redis Spark — it registers a singleton
[`IConnectionMultiplexer`](https://stackexchange.github.io/StackExchange.Redis/) you can inject anywhere.

Sparks read their configuration from the `Ignite:` section. Point the Redis Spark at your server with a
minimal `appsettings.json`:

```json
{
  "Ignite": {
    "Redis": {
      "ConnectionString": "localhost:6379"
    }
  }
}
```

> [!NOTE]
> Service configuration (like `ConnectionString`) binds directly under `Ignite:Redis`. Ignite-level
> observability toggles bind under an `Ignite:Redis:Settings` sub-node (for example
> `Ignite:Redis:Settings:HealthChecks:Enabled`). This Settings-versus-Options split is explained in
> [Core concepts](./concepts.md) and on the [Redis client integration](../ignite/sparks/stackexchange-redis.md)
> page.

Now inject the registered client in an endpoint or service:

```csharp
using StackExchange.Redis;

app.MapGet("/ping-cache", async (IConnectionMultiplexer redis) =>
{
    var db = redis.GetDatabase();
    return await db.PingAsync();
});
```

## Run it

```bash
dotnet run
```

With Ignite activated you get, out of the box:

- **Health endpoints** — a readiness endpoint (all checks, including the Redis check) and a liveness
  endpoint (only checks tagged `"live"`), mapped by `app.Ignite()`.
- **OpenTelemetry** — logging, metrics, and tracing configured, with a Redis tracing source already
  registered by the Spark.
- **Structured logs** through Serilog, and **HttpClient resilience** applied by default.

## Next steps

- [Core concepts](./concepts.md) — the Ignite two-phase model, Sparks, Settings vs Options, and
  Result/Problem.
- [Sparks catalog](../ignite/sparks/index.md) — every available integration, grouped by function.
- [Ignite overview](../ignite/index.md) — go deeper on what one `builder.Ignite(...)` call gives you.
- [Development](../development/index.md) — build, test, and contribute to the ES.FX repository itself.

## See also

- [Installation](./installation.md)
- [Quickstart](./quickstart.md)
- [Ignite overview](../ignite/index.md)
- [Redis client integration](../ignite/sparks/stackexchange-redis.md)
- [Application hosting](../development/hosting.md)
