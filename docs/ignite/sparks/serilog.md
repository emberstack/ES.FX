---
title: Serilog integration
description: Replace the host logger with Serilog through Ignite, with default enrichers and appsettings-driven configuration.
---

## Overview

The Serilog Spark wires [Serilog](https://serilog.net/) into your host as the logging backend. Call
`builder.IgniteSerilog()` once and every `ILogger<T>` in your app writes through Serilog — with a set of
sensible default enrichers, destructuring limits, and full `appsettings.json`-driven configuration already
applied. Structured logs from Ignite, your Sparks, and your own code all flow through the same Serilog
pipeline.

The Spark:

- Registers Serilog as the app's logging provider via `AddSerilog`, reading sinks, minimum levels, and
  overrides from your configuration with `ReadFrom.Configuration`.
- Applies a default logger configuration (verbose minimum level, destructuring limits, and enrichers for
  machine name, environment name, entry assembly, and application name) unless you opt out.
- Lets DI-registered Serilog services (such as `ILogEventEnricher` singletons) participate through
  `ReadFrom.Services`.

> [!TIP]
> This is the **Ignite Spark**. It configures the *host* logger for your application. For the separate,
> Ignite-free helpers on `ProgramEntryBuilder` (the `UseSerilog()` bootstrap logger) and the reusable
> enrichers, see the [Serilog additions](../../additions/serilog.md) page.

> [!NOTE]
> This Spark departs from the usual Spark shape. It has **no `SparkSettings`, no `SparkOptions`, no
> `name`/`serviceKey` parameters, and no health check** — Serilog is a global logging backend, not a keyed
> service. Serilog itself is configured through the standard `Serilog:` configuration section (read via
> `ReadFrom.Configuration`), not an `Ignite:Serilog` section.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Serilog
```

```xml
<PackageReference Include="ES.FX.Ignite.Serilog" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteSerilog` on your host application builder, before `builder.Ignite()` so that the rest of Ignite
logs through Serilog from the start:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.IgniteSerilog();
builder.Ignite();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteSerilog(
    this IHostApplicationBuilder builder,
    Action<LoggerConfiguration>? configureLoggerConfiguration = null,
    bool applyDefaultConfiguration = true,
    bool writeToProviders = true);
```

| Parameter | Type | Default | Purpose |
| --- | --- | --- | --- |
| `configureLoggerConfiguration` | `Action<LoggerConfiguration>?` | `null` | Customize the `LoggerConfiguration` used to build the logger. Runs **after** the default configuration and `ReadFrom.Configuration`, so it has the final say. |
| `applyDefaultConfiguration` | `bool` | `true` | Apply the default enrichers and destructuring limits (below) before your customization. Set `false` to start from a bare configuration. |
| `writeToProviders` | `bool` | `true` | Also write events to `ILoggerProvider`s registered through `Microsoft.Extensions.Logging` (not just Serilog sinks). |

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| Serilog logging provider | Singleton | Registered via `AddSerilog`; becomes the backend for every `ILogger` / `ILogger<T>`. |
| `ILogEventEnricher` (`ApplicationNameEnricher`) | Singleton | Added only when `applyDefaultConfiguration` is `true`; stamps the `ApplicationName` property from `IHostEnvironment`. |

When `applyDefaultConfiguration` is `true`, the Spark applies this default `LoggerConfiguration` **before**
`ReadFrom.Configuration` and your delegate:

- `MinimumLevel.Verbose()`
- Destructuring limits: max collection count `64`, max string length `2048`, max depth `16`.
- Enrichers: `FromLogContext`, `WithMachineName`, `WithEnvironmentName`, and the ES.FX
  `EntryAssemblyNameEnricher` (adds `ApplicationEntryAssembly`). The `ApplicationNameEnricher` is added
  through DI (adds `ApplicationName`).

The Spark always applies, regardless of `applyDefaultConfiguration`:

- `ReadFrom.Services(services)` — lets DI-registered Serilog components (e.g. `ILogEventEnricher`) join the
  pipeline.
- `ReadFrom.Configuration(builder.Configuration)` — binds sinks, levels, and overrides from configuration.

> [!WARNING]
> Calling `IgniteSerilog` more than once throws `ReconfigurationNotSupportedException`. It guards on a
> fixed `"Serilog"` key, so register it exactly once per host.

### Consume the logger

Inject `ILogger<T>` as usual — no Serilog-specific types needed. The messages flow through Serilog:

```csharp
public sealed class OrderService(ILogger<OrderService> logger)
{
    public void Place(int orderId)
    {
        logger.LogInformation("Placing order {OrderId}", orderId);
    }
}
```

## Configuration

Unlike other Sparks, the Serilog Spark has no `Ignite:Serilog` section, no `SparkSettings`, and no
`SparkOptions`. Serilog is configured two ways, both applied by the Spark:

1. **Declaratively**, through the standard [Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration)
   `Serilog:` section that `ReadFrom.Configuration` reads.
2. **Imperatively**, through the `configureLoggerConfiguration` delegate.

The delegate runs **after** the default configuration and `ReadFrom.Configuration`, so a value set in the
delegate overrides the JSON.

### Configure via appsettings

Configure sinks, minimum levels, and overrides in the `Serilog` section (this is Serilog's own schema, not
an Ignite one):

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" }
    ]
  }
}
```

> [!NOTE]
> The Serilog Spark bundles the common sinks — **Console**, **OpenTelemetry**, and **Seq** — so those
> sinks resolve from configuration without adding a separate `PackageReference`. Any sink *not* bundled
> (for example `Serilog.Sinks.File`) still needs its own NuGet package. `ReadFrom.Configuration` discovers
> configured sinks from the loaded assemblies.

### Configure with delegates

Use `configureLoggerConfiguration` to add sinks or enrichers in code. It runs last, so it wins over the
`Serilog:` section:

```csharp
builder.IgniteSerilog(configureLoggerConfiguration: logger =>
{
    logger
        .WriteTo.Console()
        .Enrich.WithProperty("Service", "orders");
});
```

To start from a clean slate — skipping the default enrichers and destructuring limits — pass
`applyDefaultConfiguration: false`:

```csharp
builder.IgniteSerilog(
    configureLoggerConfiguration: logger => logger.WriteTo.Console(),
    applyDefaultConfiguration: false);
```

## Health checks

This Spark registers **no health check**. Logging is a cross-cutting backend, not a probed dependency, so
there is nothing to surface at the `app.Ignite()` health endpoints.

## Observability

### Tracing

This Spark adds no `ActivitySource`. Trace context still flows into Serilog through the default
`Enrich.FromLogContext()` enricher, so correlation properties pushed onto the log context appear on log
events. OpenTelemetry tracing is configured by [Ignite](../index.md) itself and the individual Sparks.

### Metrics

This Spark emits no metrics.

### Logging

Logging *is* what this Spark provides. After `IgniteSerilog`, every `ILogger` / `ILogger<T>` in the app —
including the logs Ignite and other Sparks emit — writes through Serilog. With `writeToProviders: true`
(the default), events also reach any `ILoggerProvider`s registered through
`Microsoft.Extensions.Logging`, so provider-based sinks keep working alongside Serilog sinks.

Every event carries the default enriched properties (`MachineName`, `EnvironmentName`,
`ApplicationEntryAssembly`, `ApplicationName`) when `applyDefaultConfiguration` is left on, which makes
structured logs easy to filter across services.

## See also

- [Serilog additions](../../additions/serilog.md) — the `ProgramEntryBuilder.UseSerilog()` bootstrap logger and reusable enrichers.
- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Seq exporter Spark](./seq-exporter.md)
- [Serilog documentation](https://serilog.net/)
