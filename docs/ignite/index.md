---
title: Ignite
description: The opinionated ES.FX bootstrap — one builder.Ignite() call wires OpenTelemetry, health checks, resilience, and config; app.Ignite() finalizes the host.
---

Ignite is the "just add water" application bootstrap for ES.FX. A single `builder.Ignite(...)`
call on your host builder wires OpenTelemetry (logging, metrics, tracing), health checks,
HttpClient resilience, ProblemDetails, and a rooted configuration model — then `app.Ignite()`
finalizes middleware and health-check endpoints on the built host. Services plug in as **Sparks**:
self-contained integrations that bind config, register a client, add a health check, and wire
tracing from one `builder.Ignite{Service}...()` line.

Ignite builds on top of `ES.FX.Ignite.Spark` (the shared base) and the individual Spark packages.
You take Ignite for the cross-cutting wiring, then add only the Sparks you need.

## What Ignite gives you

One `builder.Ignite()` call adds all of the following, each toggleable through configuration:

- **OpenTelemetry** — logging, metrics, and tracing in one pipeline. HttpClient and ASP.NET Core
  instrumentation are added by default; runtime metrics instrumentation is opt-in
  (`Runtime.Metrics.Enabled` is `false` by default). The OTLP exporter is on, Azure Monitor is
  opt-in. The resource service name is set to your host's `ApplicationName`.
- **Health checks** — the health-check service is registered, and `app.Ignite()` maps readiness
  and liveness endpoints (web hosts only). Sparks contribute their own checks.
- **HttpClient resilience** — the standard resilience handler is applied to all `HttpClient`
  instances by default via `ConfigureHttpClientDefaults`.
- **ASP.NET Core services** — forwarded headers, the endpoints API explorer, `ProblemDetails`, and
  (optionally) a JSON string-enum converter. `app.Ignite()` adds the matching middleware and a set
  of response helpers (`ServerTimingMiddleware`, `QueryStringToHeaderMiddleware`,
  `TraceIdResponseHeaderMiddleware` from [Additions.Microsoft.AspNetCore](../additions/microsoft-aspnetcore.md)).
- **A rooted configuration model** — everything Ignite and its Sparks read lives under a single
  `Ignite:` section (see [Ignite configuration model](./configuration.md)).

> [!NOTE]
> Ignite is host-agnostic. The web-only pieces (middleware, health endpoints) run only when the
> host is a `WebApplication`. Worker and console hosts get everything else — OpenTelemetry, health
> checks, resilience, config — with no web middleware.

## The two-phase model

Ignite activates in two phases, split across the `builder.Build()` boundary. Both phases key off a
single `IgniteSettings` singleton.

**Phase A — pre-build, on `IHostApplicationBuilder`.** Call `builder.Ignite(...)` after creating
the host builder and before `builder.Build()`. This binds `IgniteSettings`, sets up OpenTelemetry,
health checks, HttpClient resilience, and the ASP.NET Core services. Register your Sparks
(`builder.Ignite{Service}...()`) in this phase too.

```csharp
public static void Ignite(this IHostApplicationBuilder builder,
    Action<IgniteSettings>? configureSettings = null,
    string configurationSectionPath = IgniteConfigurationSections.Ignite)
```

**Phase B — post-build, on `IHost`.** Call `app.Ignite()` on the built host. For a `WebApplication`
this wires the middleware pipeline and maps the health-check endpoints. For non-web hosts it is a
no-op beyond resolving settings, so it is safe to call everywhere.

```csharp
public static IHost Ignite(this IHost host)
```

A minimal web host looks like this:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();          // Phase A — pre-build
builder.IgniteRedisClient(); // add a Spark

var app = builder.Build();
app.Ignite();              // Phase B — post-build

await app.RunAsync();
```

> [!IMPORTANT]
> `builder.Ignite(...)` and `app.Ignite()` are two different extension methods that share a name —
> one extends `IHostApplicationBuilder` (pre-build), the other extends `IHost` (post-build). Call
> both, in that order.

> [!WARNING]
> Calling `builder.Ignite(...)` twice throws `ReconfigurationNotSupportedException`. Ignite guards a
> fixed configuration key on first activation, so a second call is rejected even if you pass a
> different `configurationSectionPath`. Each Spark applies the same one-time guard to its own key.

Some Sparks add their own **post-build** step alongside `app.Ignite()`. For example, the
[NSwag Spark](./sparks/nswag.md) exposes `app.IgniteNSwag()`, called after `app.Ignite()`:

```csharp
var app = builder.Build();
app.Ignite();
app.IgniteNSwag();   // Spark-specific post-build step
```

## Configure the core

`builder.Ignite(...)` accepts an optional `Action<IgniteSettings>` delegate. Use it to toggle the
core features in code. The delegate runs **after** configuration is read, so it overrides
`appsettings.json`.

```csharp
builder.Ignite(settings =>
{
    settings.AspNetCore.JsonStringEnumConverterEnabled = true;
    settings.OpenTelemetry.UseAzureMonitor = true;
});
```

The equivalent `appsettings.json`. Core Ignite settings bind from the `Ignite:Settings` sub-node
(the `:Settings` node is a convention shared with every Spark):

```json
{
  "Ignite": {
    "Settings": {
      "OpenTelemetry": {
        "Enabled": true,
        "UseOtlpExporter": true,
        "UseAzureMonitor": false
      },
      "AspNetCore": {
        "JsonStringEnumConverterEnabled": true,
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

`IgniteSettings` groups the core toggles into four areas — `Runtime`, `OpenTelemetry`, `HttpClient`,
and `AspNetCore`. See [Ignite configuration model](./configuration.md) for the full tree, the
Settings-vs-Options split, and the readiness/liveness tagging rules.

## The Ignite: config root

Every Ignite setting and every Spark reads from a single `Ignite:` section. Core settings live at
`Ignite:Settings`; each Spark nests under `Ignite:{Service}` (client Sparks) or
`Ignite:Services:{Service}` (service Sparks). This keeps all framework configuration in one
predictable place.

```json
{
  "Ignite": {
    "Settings": { "OpenTelemetry": { "UseOtlpExporter": true } },
    "Redis": { "ConnectionString": "localhost:6379" }
  }
}
```

The `configurationSectionPath` parameter (default `"Ignite"`) lets you move the whole tree, but most
applications never change it. The full model, including the Options-vs-Settings binding paths, is
covered in [Ignite configuration model](./configuration.md).

## Add Sparks

A **Spark** plugs a service into Ignite. Register one between `builder.Ignite(...)` and
`builder.Build()`:

```csharp
builder.Ignite();
builder.IgniteRedisClient();   // registers IConnectionMultiplexer + health check + tracing
```

That one line binds the service's configuration, registers its client in DI (keyed-capable), adds a
health check, and wires OpenTelemetry — no extra plumbing. Browse the full list in the
[Sparks catalog](./sparks/index.md), or read the canonical
[Redis Spark page](./sparks/stackexchange-redis.md) to see the shape end to end. To author your own,
see [Creating a Spark](./creating-a-spark.md).

## Health check endpoints

When the host is a `WebApplication` and health checks are enabled, `app.Ignite()` maps two
endpoints:

- **Readiness** — `/health/ready` by default. **All** registered health checks must pass. Use this
  to decide whether the app is ready to accept traffic.
- **Liveness** — `/health/live` by default. Only checks tagged `"live"` gate this endpoint. Use it
  to decide whether the app is still running.

Spark health checks are **readiness-only** by default; they gate liveness only when their
`Settings.HealthChecks.Tags` include `"live"`. Both paths are configurable through
`AspNetCore.HealthChecks.ReadinessEndpointPath` / `LivenessEndpointPath`, and HTTP metrics are
suppressed on them when `AspNetCore.Metrics.HealthChecksFiltered` is set.

```text
GET /health/ready   → readiness (all checks)
GET /health/live    → liveness  (checks tagged "live")
```

> [!NOTE]
> Non-web hosts (workers, consoles) still register the health-check service, but no endpoints are
> mapped — there is no HTTP surface to map them onto.

## See also

- [Ignite configuration model](./configuration.md) — the `Ignite:` tree, Settings vs Options, and health-check tags.
- [Sparks catalog](./sparks/index.md) — every available Spark, grouped by function.
- [Creating a Spark](./creating-a-spark.md) — author your own integration.
- [Quickstart](../getting-started/quickstart.md) — build your first Ignite app end to end.
- [Application hosting](../development/hosting.md) — the `ProgramEntry` lifecycle wrapper Ignite runs inside.
