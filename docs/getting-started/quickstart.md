---
title: Quickstart
description: Build your first Ignite-powered ASP.NET Core API end-to-end, with OpenTelemetry, health checks, and a Redis client wired up.
---

This quickstart builds a working ASP.NET Core API host from scratch using **Ignite**, the "just add water"
bootstrap. In a few minutes you get structured startup and shutdown, OpenTelemetry (logging, metrics,
tracing), health check endpoints, HttpClient resilience, and a Redis client — all from a handful of lines.

By the end you will have a `Program.cs` that follows the same composition pattern as the ES.FX playground:
`ProgramEntry` wraps `Main`, `builder.Ignite()` bootstraps the app pre-build, and `app.Ignite()` finalizes
middleware and health endpoints post-build.

> [!IMPORTANT]
> Ignite activation is **two-phase**. You call `builder.Ignite(...)` on the `IHostApplicationBuilder`
> before `builder.Build()`, then `app.Ignite()` on the built `IHost` after. Web-only middleware (forwarded
> headers, exception handling, health endpoints) is wired in the second phase and only for `WebApplication`
> hosts.

## Prerequisites

- **.NET 10 SDK** installed.
- An IDE or editor (Visual Studio, Rider, or VS Code).
- A running **Redis** instance for the Spark in this walkthrough (for example
  `localhost:6379`). If you do not have one handy, you can skip the Redis section — the API still runs
  without it.

## Create the project

Create a minimal ASP.NET Core project to host the API:

```bash
dotnet new web -n MyService
cd MyService
```

## Install the packages

Add the Ignite bootstrap, the Serilog integration used by the entry point, and the Redis Spark:

```bash
dotnet add package ES.FX.Ignite
dotnet add package ES.FX.Additions.Serilog
dotnet add package ES.FX.Ignite.Serilog
dotnet add package ES.FX.Ignite.StackExchange.Redis
```

The equivalent `<PackageReference>` entries:

```xml
<PackageReference Include="ES.FX.Ignite" Version="..." />
<PackageReference Include="ES.FX.Additions.Serilog" Version="..." />
<PackageReference Include="ES.FX.Ignite.Serilog" Version="..." />
<PackageReference Include="ES.FX.Ignite.StackExchange.Redis" Version="..." />
```

> [!NOTE]
> Inside the ES.FX repository, Central Package Management pins every version in
> `Directory.Packages.props`, so in-repo `<PackageReference>` entries carry no `Version` attribute. In your
> own consuming project set the `Version` explicitly (shown as `...` above) or centralize versions yourself.

> [!TIP]
> `ProgramEntry.UseSerilog()` comes from `ES.FX.Additions.Serilog` — it is not part of `ES.FX.Hosting`.
> `ES.FX.Ignite.Serilog` is a separate Spark that routes the host's logging through Serilog. Both are
> optional, but the playground uses them together.

## Write the bootstrap

Replace the generated `Program.cs` with the composition below. This is the same shape the
`Playground.Microservice.Api.Host` uses: `ProgramEntry.CreateBuilder(args)` wraps everything in structured
startup/shutdown and error handling, `WebApplication.CreateBuilder(args)` creates the host builder,
`builder.Ignite(...)` bootstraps it, Sparks plug in services, then `app.Ignite()` finalizes the app.

```csharp
using ES.FX.Hosting.Lifetime;
using ES.FX.Additions.Serilog.Lifetime;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Ignite.StackExchange.Redis.Hosting;
using StackExchange.Redis;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);

    // Route the host's logging through Serilog.
    builder.Logging.ClearProviders();
    builder.IgniteSerilog();

    // Phase A — bootstrap Ignite on the host builder (pre-build).
    builder.Ignite(settings =>
    {
        settings.AspNetCore.JsonStringEnumConverterEnabled = true;
    });

    // Add a Spark: a shared IConnectionMultiplexer with health checks and tracing wired up.
    builder.IgniteRedisClient();

    var app = builder.Build();

    // Phase B — finalize middleware and health endpoints (post-build).
    app.Ignite();

    // A minimal endpoint that uses the registered Redis client.
    app.MapGet("/ping", async (IConnectionMultiplexer redis) =>
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync("quickstart:ping", "pong");
        var value = await db.StringGetAsync("quickstart:ping");
        return Results.Ok(new { value = value.ToString() });
    });

    await app.RunAsync();
    return 0;
});
```

What each piece does:

| Line | Role |
| --- | --- |
| `ProgramEntry.CreateBuilder(args).UseSerilog().Build()` | Wraps `Main` with structured logging, error handling, and graceful shutdown. `UseSerilog()` supplies the bootstrap logger. |
| `.RunAsync(async _ => { ... return 0; })` | Runs your composition; the returned `int` is the process exit code. Uncaught exceptions are logged and become exit code `1`. |
| `builder.Ignite(...)` | Phase A. Adds OpenTelemetry, health checks, HttpClient resilience, ProblemDetails, and the JSON string-enum converter. |
| `builder.IgniteRedisClient()` | Registers `IConnectionMultiplexer` as a singleton, plus a Redis health check and a tracing source. |
| `app.Ignite()` | Phase B. For a `WebApplication`, wires forwarded-headers, exception handling, and the health check endpoints. |

> [!NOTE]
> A worker or console host uses `Host.CreateApplicationBuilder(args)` instead of
> `WebApplication.CreateBuilder(args)` and still calls `builder.Ignite()` then `app.Ignite()` — it simply
> gets no web middleware. The two-phase pattern is identical.

## Configure it

Ignite reads its configuration from the rooted `Ignite:` section. Add an `appsettings.json` block for the
Redis connection and the Ignite-level toggles.

> [!IMPORTANT]
> **Options** (the service's own configuration, such as `ConnectionString`) bind at the Spark path
> directly — `Ignite:Redis:ConnectionString`. **Settings** (Ignite observability toggles) bind under a
> `:Settings` sub-node — `Ignite:Redis:Settings:HealthChecks:Enabled`. Keep the two apart.

```json
{
  "Ignite": {
    "Redis": {
      "ConnectionString": "localhost:6379",
      "Settings": {
        "HealthChecks": {
          "Enabled": true,
          "Timeout": "00:00:05"
        },
        "Tracing": {
          "Enabled": true
        }
      }
    }
  }
}
```

You can configure the same values inline with delegates instead. Delegates run **after** `appsettings.json`
is read, so they override it:

```csharp
builder.IgniteRedisClient(
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
        settings.Tracing.Enabled = true;
    },
    configureOptions: options =>
    {
        options.ConnectionString = "localhost:6379";
    });
```

## Run it

Start the host:

```bash
dotnet run
```

Out of the box you now have:

- **Structured logs** on the console via Serilog, from process start through graceful shutdown.
- **OpenTelemetry** logging, metrics, and tracing (including a Redis tracing source), ready to export.
- **Health check endpoints** mapped by `app.Ignite()` — a readiness endpoint (all checks must pass) and a
  liveness endpoint (only checks tagged `"live"`). The Redis Spark's check surfaces on readiness.
- **HttpClient resilience**, ProblemDetails error responses, and forwarded-headers handling.

Call the endpoint you mapped:

```bash
curl http://localhost:5000/ping
```

```json
{ "value": "pong" }
```

> [!TIP]
> Registering the same Spark twice for the same key throws `ReconfigurationNotSupportedException`. Call
> `builder.IgniteRedisClient()` once per instance; use keyed registrations (a `serviceKey`) when you need
> more than one Redis client.

## Next steps

- Go deeper on the two-phase model and the core toggles in the [Ignite overview](../ignite/index.md).
- Understand how configuration is rooted and split in the
  [Ignite configuration model](../ignite/configuration.md).
- Browse every integration in the [Sparks catalog](../ignite/sparks/index.md), starting with the
  [Redis Spark](../ignite/sparks/stackexchange-redis.md) you just used.
- Learn the vocabulary — Ignite, Sparks, Settings vs Options, Result/Problem — in
  [Core concepts](./concepts.md).

## See also

- [Installation](./installation.md)
- [Core concepts](./concepts.md)
- [Ignite overview](../ignite/index.md)
- [Redis client integration](../ignite/sparks/stackexchange-redis.md)
- [Development guide](../development/index.md)
