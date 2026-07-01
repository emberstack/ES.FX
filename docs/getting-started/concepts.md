---
title: Core concepts
description: The ES.FX vocabulary you need before building — the two-phase Ignite model, Sparks, Settings vs Options, and the Result/Problem primitives.
---

Before you build with ES.FX, it helps to learn a handful of concepts that recur across the whole
framework. This page introduces each one, shows where it fits, and ends by assembling them into a
complete, copy-pasteable Ignite API host. Every term here has a dedicated page that goes deeper — the
goal now is a working mental model, not exhaustive reference.

## Layers you consume independently

ES.FX ships as small `ES.FX.*` NuGet packages arranged in layers. You take only the layer you need:

- **`ES.FX`** — framework-agnostic core primitives: `Result`/`Problem` error handling, `Optional<T>`,
  and BCL-style helpers. No dependencies on anything else in the framework.
- **`ES.FX.Additions.*`** — focused helpers, each augmenting exactly one third-party library.
- **`ES.FX.Hosting`** — a lifecycle wrapper (`ProgramEntry`) around your `Main`.
- **`ES.FX.Ignite`** — the opinionated "just add water" bootstrap, plus its pluggable **Sparks**.
- **Feature libraries** — standalone patterns such as the Transactional Outbox and Migrations.

Ignite is where most applications start, so the rest of this page focuses there.

## Ignite is two-phase

Ignite wires up OpenTelemetry, health checks, HttpClient resilience, `ProblemDetails`, and a rooted
configuration model. It activates in **two phases**, split across the `builder.Build()` boundary:

1. **Phase A — pre-build**, on the `IHostApplicationBuilder`. You call `builder.Ignite(...)` to register
   services and bind configuration.
2. **Phase B — post-build**, on the built `IHost`. You call `app.Ignite()` to finalize middleware and map
   health-check endpoints.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Ignite();     // Phase A — IHostApplicationBuilder
var app = builder.Build();
app.Ignite();         // Phase B — IHost
```

Both calls are named `Ignite`, but they are distinct extension methods on different types. The Phase A
signature is:

```csharp
public static void Ignite(this IHostApplicationBuilder builder,
    Action<IgniteSettings>? configureSettings = null,
    string configurationSectionPath = IgniteConfigurationSections.Ignite);
```

> [!IMPORTANT]
> Order matters. All `builder.Ignite*` calls (the core `Ignite` and every Spark) happen **between**
> `WebApplication.CreateBuilder(args)` and `builder.Build()`. `app.Ignite()` and any post-build Spark
> steps run **after** `builder.Build()`.

> [!NOTE]
> Phase B only wires web middleware and health endpoints when the host is a `WebApplication`. Worker and
> console hosts built with `Host.CreateApplicationBuilder(args)` still call `builder.Ignite()` and
> `app.Ignite()` — they just get no web middleware.

See [Ignite overview](../ignite/index.md) for the full list of what each phase configures.

## A Spark is one integration

A **Spark** is a self-contained integration that plugs a single service into Ignite. One call — for
example `builder.IgniteRedisClient()` — binds that service's configuration, registers it in DI
(optionally keyed), adds a health check, and wires OpenTelemetry tracing. You add a Spark in Phase A,
right after `builder.Ignite()`:

```csharp
builder.Ignite();
builder.IgniteRedisClient();   // registers IConnectionMultiplexer, with health check + tracing
```

A few Sparks also contribute a post-build step (for example `app.IgniteNSwag()`), which you call after
`app.Ignite()`. Each Spark's page tells you if it has one.

> [!WARNING]
> Registering the same Spark twice for the same key throws `ReconfigurationNotSupportedException`. Use a
> distinct `serviceKey` for each instance (see the keyed clients section on a Spark page).

Browse the full [Sparks catalog](../ignite/sparks/index.md) for every available integration.

## Settings vs Options

Each Spark exposes **two** kinds of configuration, and keeping them straight is the single most useful
thing to internalize:

| Concept | Type | What it controls | Customized via |
| --- | --- | --- | --- |
| **Options** | `{Service}SparkOptions` | The underlying service's own configuration (for Redis, `ConnectionString` / native `ConfigurationOptions`). | `configureOptions` delegate |
| **Settings** | `{Service}SparkSettings` | Ignite-level observability toggles: `HealthChecks`, `Tracing`, and (where present) `Metrics`. | `configureSettings` delegate |

Both bind from configuration, but from **different nodes** under the Spark's section. Options bind the
section path directly; Settings bind a `:Settings` sub-node:

```json
{
  "Ignite": {
    "Redis": {
      "ConnectionString": "localhost:6379",
      "Settings": {
        "HealthChecks": { "Enabled": true },
        "Tracing": { "Enabled": true }
      }
    }
  }
}
```

The delegates run **after** configuration is read, so a delegate overrides what `appsettings.json`
provides:

```csharp
builder.IgniteRedisClient(
    configureSettings: settings =>
    {
        settings.Tracing.Enabled = true;
    },
    configureOptions: options =>
    {
        options.ConnectionString = "localhost:6379";
    });
```

The [Ignite configuration model](../ignite/configuration.md) page covers the `Ignite:` root, the
`:Settings` sub-node, and named/keyed instances in full.

## Health checks: readiness vs liveness

`app.Ignite()` maps two health endpoints on a `WebApplication`:

- **Readiness** (`/health/ready`) — **all** registered health checks must pass.
- **Liveness** (`/health/live`) — only checks tagged `"live"` must pass.

A Spark's health check contributes to readiness by default; it participates in liveness only if its
`Settings.HealthChecks.Tags` includes `"live"`. Both endpoint paths are configurable through Ignite's
core `AspNetCore.HealthChecks` settings.

## Result and Problem

Away from hosting, the core `ES.FX` package gives you a result pattern for modeling success-or-failure
without throwing. `Result<T>` holds either a value of `T` **or** a `Problem` (an RFC 7807 error shape);
the non-generic `Result` is the same for operations that only need success/failure.

```csharp
using ES.FX.Problems;
using ES.FX.Results;

Result<int> Parse(string input) =>
    int.TryParse(input, out var value)
        ? value                                   // implicit T        -> Result<int>
        : new Problem(title: "Invalid number");   // implicit Problem  -> Result<int>

var result = Parse("42");
if (result.TryPickResult(out var value, out var problem))
    Console.WriteLine($"Parsed {value}");
else
    Console.WriteLine($"Failed: {problem.Title}");
```

Both a value and a `Problem` convert implicitly into a `Result<T>`, so returning either is a one-liner.
See [Results and Problems](../development/results-and-problems.md) and [Primitives](../development/primitives.md)
for the complete surface, including `ValidationProblem` and `Optional<T>`.

## Put it together

Here is a complete, minimal Ignite API host that uses every concept above. It follows the real playground
composition: a `ProgramEntry` wraps `Main` for structured startup and graceful shutdown, `builder.Ignite()`
turns on the framework, two Sparks plug in Redis and the Seq exporter, and `app.Ignite()` finalizes the
web pipeline.

```csharp
using ES.FX.Additions.Serilog.Lifetime;
using ES.FX.Hosting.Lifetime;
using ES.FX.Ignite.Hosting;
using ES.FX.Ignite.OpenTelemetry.Exporter.Seq.Hosting;
using ES.FX.Ignite.Serilog.Hosting;
using ES.FX.Ignite.StackExchange.Redis.Hosting;

return await ProgramEntry.CreateBuilder(args).UseSerilog().Build().RunAsync(async _ =>
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog logging (the Serilog Spark)
    builder.Logging.ClearProviders();
    builder.IgniteSerilog();

    // Phase A — activate Ignite
    builder.Ignite(settings =>
    {
        settings.AspNetCore.JsonStringEnumConverterEnabled = true;
    });

    // Add Sparks (still Phase A)
    builder.IgniteSeqOpenTelemetryExporter();
    builder.IgniteRedisClient();

    var app = builder.Build();

    // Phase B — finalize middleware and health endpoints
    app.Ignite();

    app.MapGet("/", () => "Hello from ES.FX Ignite");

    await app.RunAsync();
    return 0;
});
```

> [!NOTE]
> `UseSerilog()` on the `ProgramEntryBuilder` comes from **`ES.FX.Additions.Serilog`**, not from
> `ES.FX.Hosting`. Hosting has no built-in Serilog dependency.

A matching `appsettings.json` sets the two Sparks' configuration. Options bind directly under each
Spark's section; observability Settings bind under `:Settings`:

```json
{
  "Ignite": {
    "Redis": {
      "ConnectionString": "localhost:6379"
    },
    "OpenTelemetry": {
      "Exporter": {
        "Seq": {
          "IngestionEndpoint": "http://localhost:5341/ingest/otlp"
        }
      }
    }
  }
}
```

Run it and you get, out of the box: OpenTelemetry logs, metrics, and traces; `/health/ready` and
`/health/live` endpoints (including a Redis ping); HttpClient resilience; and `ProblemDetails` error
responses.

## Next steps

- Follow the [Quickstart](./quickstart.md) to build and run this host step by step.
- Read [Installation](./installation.md) for where the packages come from and how versions are managed.
- Go deeper on [Ignite](../ignite/index.md) and the [Ignite configuration model](../ignite/configuration.md).

## See also

- [Ignite overview](../ignite/index.md)
- [Sparks catalog](../ignite/sparks/index.md)
- [Redis client integration](../ignite/sparks/stackexchange-redis.md)
- [Application hosting](../development/hosting.md)
- [Results and Problems](../development/results-and-problems.md)
