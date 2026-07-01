---
title: Ignite configuration model
description: How Ignite reads configuration — the Ignite root, the :Settings sub-node, Settings vs Options, and health-check readiness and liveness tags.
---

Ignite reads everything it needs from a single rooted configuration section. This page describes that
tree: where the core `IgniteSettings` live, how each Spark nests under it, the crucial split between
**Settings** and **Options**, and how the health-check readiness and liveness endpoints are configured.

If you are new to Ignite, read the [Ignite overview](./index.md) first for the two-phase
`builder.Ignite()` / `app.Ignite()` model. This page assumes you already know that `builder.Ignite(...)`
runs pre-build and `app.Ignite()` runs post-build.

## The `Ignite:` configuration root

All Ignite configuration lives under a single root section, `Ignite`, in your `appsettings.json` (or any
other configuration provider). Both the core framework settings and every Spark bind from somewhere under
this root.

```json
{
  "Ignite": {
    "Settings": {
      "OpenTelemetry": { "UseAzureMonitor": true }
    },
    "Redis": {
      "ConnectionString": "localhost:6379"
    },
    "Services": {
      "MigrationsService": {
        "Settings": { "Enabled": true }
      }
    }
  }
}
```

Three kinds of things nest under the root:

| Location | Binds to | Example |
| --- | --- | --- |
| `Ignite:Settings` | The core `IgniteSettings` (framework observability, HttpClient, ASP.NET Core). | `Ignite:Settings:OpenTelemetry:UseAzureMonitor` |
| `Ignite:{Service}` | A **client Spark** rooted directly under `Ignite`. | `Ignite:Redis`, `Ignite:DbContext` |
| `Ignite:Services:{Service}` | A **service-group Spark** (hosted services). | `Ignite:Services:MigrationsService` |

> [!NOTE]
> The root section is an argument, not a hard-coded string. `builder.Ignite(...)` takes a
> `configurationSectionPath` parameter that defaults to `"Ignite"`
> (`IgniteConfigurationSections.Ignite`). Change it only if you must relocate the entire tree — most
> applications never do.

## Configure the Ignite core

The core `IgniteSettings` object controls the cross-cutting features Ignite wires up: OpenTelemetry,
the resilient `HttpClient`, and the ASP.NET Core middleware and health endpoints. It binds from the
`Ignite:Settings` sub-node.

Configure it two ways — through `appsettings.json`, or with the `configureSettings` delegate passed to
`builder.Ignite(...)`. The delegate runs **after** configuration is read, so it overrides `appsettings`.

### Configure via appsettings

```json
{
  "Ignite": {
    "Settings": {
      "OpenTelemetry": {
        "Enabled": true,
        "UseOtlpExporter": true,
        "UseAzureMonitor": false
      },
      "HttpClient": {
        "StandardResilienceHandlerEnabled": true
      },
      "AspNetCore": {
        "JsonStringEnumConverterEnabled": true,
        "HealthChecks": {
          "ReadinessEndpointPath": "/health/ready",
          "LivenessEndpointPath": "/health/live"
        }
      }
    }
  }
}
```

### Configure with a delegate

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite(settings =>
{
    settings.AspNetCore.JsonStringEnumConverterEnabled = true;
    settings.OpenTelemetry.UseAzureMonitor = true;
    settings.HttpClient.StandardResilienceHandlerEnabled = true;
});
```

> [!IMPORTANT]
> The core settings bind from `Ignite:Settings`, **not** `Ignite` directly. In JSON that means
> `Ignite:Settings:OpenTelemetry:Enabled`, not `Ignite:OpenTelemetry:Enabled`. The `:Settings` sub-node
> is easy to miss.

### Core settings reference

Every toggle below is a plain `Enabled`-style boolean, and each defaults to a sensible value so an
empty `Ignite:Settings` section still gives you a fully wired application.

| Setting path (under `Ignite:Settings`) | Default | What it controls |
| --- | --- | --- |
| `OpenTelemetry:Enabled` | `true` | Master switch for all OpenTelemetry (logging, metrics, tracing). |
| `OpenTelemetry:LoggingEnabled` | `true` | Emit logs through the OpenTelemetry logging provider. |
| `OpenTelemetry:LoggingIncludeFormattedMessage` | `true` | Include the formatted message on log records. |
| `OpenTelemetry:LoggingIncludeScopes` | `true` | Include logging scopes on log records. |
| `OpenTelemetry:UseOtlpExporter` | `true` | Export telemetry over OTLP. The exporter is only registered when an OTLP endpoint is configured (`OTEL_EXPORTER_OTLP_ENDPOINT` or a signal-specific `OTEL_EXPORTER_OTLP_{TRACES,METRICS,LOGS}_ENDPOINT`). |
| `OpenTelemetry:UseAzureMonitor` | `false` | Export telemetry to Azure Monitor. |
| `HttpClient:StandardResilienceHandlerEnabled` | `true` | Add the standard resilience handler to all `HttpClient`s. |
| `HttpClient:Tracing:Enabled` | `true` | Trace outbound `HttpClient` calls. |
| `HttpClient:Metrics:Enabled` | `true` | Emit `HttpClient` metrics. |
| `Runtime:Metrics:Enabled` | `false` | Emit .NET runtime metrics. |
| `AspNetCore:Tracing:Enabled` | `true` | Trace incoming ASP.NET Core requests. |
| `AspNetCore:Tracing:HealthChecksFiltered` | `true` | Exclude health-check probe requests from ASP.NET Core tracing. |
| `AspNetCore:Tracing:EnrichClientAddressFromRemoteIpAddress` | `true` | Set the `client.address` trace tag from `RemoteIpAddress` (correct client IP behind forwarded headers). |
| `AspNetCore:Metrics:Enabled` | `true` | Emit ASP.NET Core request metrics. |
| `AspNetCore:Metrics:HealthChecksFiltered` | `true` | Exclude health-check probe requests from ASP.NET Core metrics. |
| `AspNetCore:JsonStringEnumConverterEnabled` | `true` | Serialize enums as strings in ASP.NET Core JSON. |
| `AspNetCore:AddProblemDetails` | `true` | Register the ProblemDetails service. Disabling this also skips the exception-handler middleware unless another handler is registered (see `UseExceptionHandler`). |
| `AspNetCore:AddEndpointsApiExplorer` | `true` | Register the Endpoints API Explorer (endpoint metadata for OpenAPI). |
| `AspNetCore:ForwardedHeadersEnabled` | `true` | Honor `X-Forwarded-*` headers. |
| `AspNetCore:UseExceptionHandler` | `true` | Enable the exception-handler middleware. The middleware is only added when a handler is available: an `IProblemDetailsService` (see `AddProblemDetails`), a registered `IExceptionHandler`, or configured `ExceptionHandlerOptions`. |
| `AspNetCore:UseStatusCodePages` | `true` | Enable the status-code-pages middleware. |
| `AspNetCore:UseDeveloperExceptionPage` | `true` | Enable the developer exception page in the Development environment. |
| `AspNetCore:UseQueryStringToHeaderMiddleware` | `true` | Enable the query-string-to-header middleware. |
| `AspNetCore:UseServerTimingMiddleware` | `true` | Enable the `Server-Timing` response-header middleware. |
| `AspNetCore:UseTraceIdResponseHeader` | `true` | Enable the trace-id response-header middleware. |

> [!NOTE]
> The `AspNetCore:*` settings only take effect for `WebApplication` hosts. Worker and console hosts
> read the same configuration but skip the web middleware in `app.Ignite()`.

## Settings vs Options

Ignite splits configuration into two distinct concepts. Keeping them straight is essential when reading
any Spark's configuration:

| Concept | Type | Purpose | Binds from | Customized via |
| --- | --- | --- | --- | --- |
| **Settings** | `{Service}SparkSettings` | Ignite-level observability toggles: `HealthChecks`, `Tracing`, `Metrics`. | `{path}:Settings` | `configureSettings` delegate |
| **Options** | `{Service}SparkOptions` | The underlying service's own configuration (e.g. `ConnectionString`). | `{path}` directly | `configureOptions` delegate |

The difference in binding path is the part people trip over. For the Redis Spark, whose section path is
`Ignite:Redis`:

- **Options** bind at the section path directly. The connection string is
  `Ignite:Redis:ConnectionString`.
- **Settings** bind at the `:Settings` sub-node. The health-check toggle is
  `Ignite:Redis:Settings:HealthChecks:Enabled`.

```json
{
  "Ignite": {
    "Redis": {
      "ConnectionString": "localhost:6379",
      "Settings": {
        "HealthChecks": { "Enabled": true, "Timeout": "00:00:05" },
        "Tracing": { "Enabled": true }
      }
    }
  }
}
```

Both delegates run after configuration is read, so a delegate overrides `appsettings.json`:

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

> [!NOTE]
> This split is the same for the core: the framework's own `Ignite:Settings` node is the `Settings`
> half of the `Ignite` root. Core Ignite has no separate `Options` type — it is configured entirely
> through `IgniteSettings`.

For the full anatomy of a Spark's Settings and Options types, see
[Creating a Spark](./creating-a-spark.md).

## Named and keyed instances

Every Spark registration takes a `name` and a `serviceKey`, and they do different jobs:

- **`name`** selects the configuration sub-section. With `name: "cache"`, the Redis Spark reads from
  `Ignite:Redis:cache` (Options) and `Ignite:Redis:cache:Settings` (Settings) instead of
  `Ignite:Redis`. It does not affect DI.
- **`serviceKey`** registers the service as a keyed singleton, resolved with `[FromKeyedServices("…")]`.
  When `null`, the service is the default (unkeyed) registration.

```json
{
  "Ignite": {
    "Redis": {
      "cache": { "ConnectionString": "localhost:6379" },
      "session": { "ConnectionString": "localhost:6380" }
    }
  }
}
```

```csharp
builder.IgniteRedisClient(name: "cache", serviceKey: "cache");
builder.IgniteRedisClient(name: "session", serviceKey: "session");
```

> [!WARNING]
> Registering the same Spark twice for the same key throws `ReconfigurationNotSupportedException`.
> Ignite guards each registration by key (the Spark name plus `serviceKey`) to prevent accidental
> double-configuration. Use distinct `serviceKey` values for multiple instances.

## Health-check endpoints

`app.Ignite()` maps two health-check endpoints on `WebApplication` hosts, distinguished by which checks
they include:

| Endpoint | Default path | Which checks gate it |
| --- | --- | --- |
| **Readiness** | `/health/ready` | **All** registered health checks must pass. |
| **Liveness** | `/health/live` | Only checks tagged `"live"` must pass. |

Readiness answers "is the app ready to accept traffic?" — every dependency check counts. Liveness
answers "is the app still running?" — only checks explicitly tagged `"live"` count, so a failing
downstream dependency does not cause the process to be restarted.

> [!IMPORTANT]
> Spark health checks are **readiness-only by default**. A Spark's check carries its own identifying tag
> (for example the Redis check is tagged `"Redis"`) but is not tagged `"live"` unless you add `"live"` to
> its `HealthChecks.Tags`, so it participates in `/health/ready` but not `/health/live`. Tag a check
> `"live"` only when its failure genuinely means the process is dead.

Configure the endpoints through the core ASP.NET Core settings:

```json
{
  "Ignite": {
    "Settings": {
      "AspNetCore": {
        "HealthChecks": {
          "Enabled": true,
          "ReadinessEndpointPath": "/health/ready",
          "LivenessEndpointPath": "/health/live"
        }
      }
    }
  }
}
```

```csharp
builder.Ignite(settings =>
{
    settings.AspNetCore.HealthChecks.Enabled = true;
    settings.AspNetCore.HealthChecks.ReadinessEndpointPath = "/health/ready";
    settings.AspNetCore.HealthChecks.LivenessEndpointPath = "/health/live";
});
```

Setting `AspNetCore.HealthChecks.Enabled` to `false` skips mapping both endpoints entirely. By default,
ASP.NET Core request tracing and metrics filter out these health-check paths
(`AspNetCore.Tracing.HealthChecksFiltered` and `AspNetCore.Metrics.HealthChecksFiltered`, both `true`),
so probe traffic does not pollute your telemetry.

To tag a Spark's health check for liveness, add the tag through its `configureSettings` delegate:

```csharp
builder.IgniteRedisClient(configureSettings: settings =>
{
    settings.HealthChecks.Tags = ["live"];
});
```

## See also

- [Ignite overview](./index.md) — the two-phase `builder.Ignite()` / `app.Ignite()` model.
- [Creating a Spark](./creating-a-spark.md) — authoring the `{Service}SparkSettings` and `{Service}SparkOptions` types.
- [Sparks catalog](./sparks/index.md) — every available Spark and its configuration.
- [Redis client integration](./sparks/stackexchange-redis.md) — the canonical Spark, used as the running example here.
- [Seq OpenTelemetry exporter](./sparks/seq-exporter.md) — ship the telemetry Ignite produces to Seq.
