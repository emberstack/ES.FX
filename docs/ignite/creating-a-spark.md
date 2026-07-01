---
title: Creating a Spark
description: Author your own Ignite Spark by following the fixed anatomy — Spark definition, Settings, Options, hosting extension, and health check.
---

A **Spark** is a self-contained Ignite integration: it binds configuration from the `Ignite:` root,
registers a service into DI (keyed-capable), adds a health check, and wires OpenTelemetry — all from a
single `builder.Ignite{Service}...()` call. Every Spark in the repository shares the same fixed shape, so
once you know one, you know them all. This page walks that shape as an authoring checklist, using the
canonical Redis Spark (`ES.FX.Ignite.StackExchange.Redis`) as the reference implementation.

Before you start, make sure you understand the pieces a Spark plugs into: the
[two-phase Ignite lifecycle](./index.md) and the [Settings-vs-Options configuration model](./configuration.md).

## The fixed Spark shape

A Spark package for a service named `{Service}` contains five parts. Mirror this layout — reviewers and
users expect it, and the [Sparks catalog](./sparks/index.md) documents Sparks against exactly this anatomy.

| Part | File | Responsibility |
| --- | --- | --- |
| **Spark definition** | `{Service}Spark.cs` | Holds the `Name` and `ConfigurationSectionPath` constants. |
| **Options** | `Configuration/{Service}SparkOptions.cs` | The underlying service configuration (connection string, native options). |
| **Settings** | `Configuration/{Service}SparkSettings.cs` | Ignite observability toggles: `HealthChecks`, `Tracing`, optionally `Metrics`. |
| **Hosting extension** | `Hosting/{Service}HostingExtensions.cs` | The public `Ignite{Service}...()` entry point on `IHostApplicationBuilder`. |
| **Health check** | `HealthChecks/` | The `IHealthCheck` implementation, where applicable. |

> [!IMPORTANT]
> **Settings** and **Options** are separate types with separate delegates. Settings are Ignite-level
> observability toggles bound from the `:Settings` sub-node; Options are the service's own configuration
> bound from the section directly. Never merge them. See [Ignite configuration](./configuration.md) for
> the full model.

Reference the shared base package from your Spark project:

```bash
dotnet add package ES.FX.Ignite.Spark
```

```xml
<PackageReference Include="ES.FX.Ignite.Spark" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so a `<PackageReference>` inside this repository carries no
> `Version` attribute — versions are pinned centrally in `Directory.Packages.props`. See
> [Conventions & build config](../development/conventions.md).

## Step 1 — Define the Spark

Create a static `{Service}Spark` class holding two constants: the Spark `Name` and the default
`ConfigurationSectionPath`. The path is built from the shared `IgniteConfigurationSections.Ignite` root
so every Spark nests under `Ignite:`.

```csharp
using ES.FX.Ignite.Spark.Configuration;

namespace ES.FX.Ignite.StackExchange.Redis;

public static class RedisSpark
{
    /// <summary>Spark name</summary>
    public const string Name = "Redis";

    /// <summary>The default configuration section path</summary>
    public const string ConfigurationSectionPath = $"{IgniteConfigurationSections.Ignite}:{Name}";
}
```

For Redis this resolves to `Ignite:Redis`. Client Sparks sit directly under the root like this. Sparks
that represent a background *service* instead nest under `IgniteConfigurationSections.Services`
(`Ignite:Services`) — for example the [Migrations runner](./sparks/migrations.md) uses
`Ignite:Services:MigrationsService`.

## Step 2 — Define the Options

`{Service}SparkOptions` is the **underlying service configuration** — everything the service itself needs
to run. Keep it plain: bindable properties only, no observability concerns. For Redis, that is the
connection string and the native `ConfigurationOptions`.

```csharp
using StackExchange.Redis;

namespace ES.FX.Ignite.StackExchange.Redis.Configuration;

public class RedisSparkOptions
{
    /// <summary>The connection string for <see cref="IConnectionMultiplexer" /></summary>
    public string? ConnectionString { get; set; }

    /// <summary>The configuration options for <see cref="IConnectionMultiplexer" /></summary>
    public ConfigurationOptions? ConfigurationOptions { get; set; }
}
```

Options bind from the section path **directly** (`Ignite:Redis:ConnectionString`).

## Step 3 — Define the Settings

`{Service}SparkSettings` holds the **Ignite observability toggles**. Compose from the shared base types in
`ES.FX.Ignite.Spark.Configuration` so every Spark exposes the same knobs the same way:

- `HealthCheckSettings HealthChecks` — `Enabled` (defaults to `true`), `FailureStatus`, `Timeout`, `Tags`.
- `TracingSettings Tracing` — `Enabled` (defaults to `true`).
- `MetricsSettings Metrics` — `Enabled` (defaults to `true`). Add this only if your Spark emits metrics.

```csharp
using ES.FX.Ignite.Spark.Configuration;
using ES.FX.Ignite.Spark.Configuration.OpenTelemetry;

namespace ES.FX.Ignite.StackExchange.Redis.Configuration;

public class RedisSparkSettings
{
    public HealthCheckSettings HealthChecks { get; set; } = new();

    public TracingSettings Tracing { get; set; } = new();
}
```

Settings bind from the **`:Settings` sub-node** of the section path — so
`Ignite:Redis:Settings:HealthChecks:Enabled`, not `Ignite:Redis:HealthChecks:Enabled`. This split is
enforced by `SparkConfig.GetSettings`, which binds `"{path}:Settings"`. Getting the node wrong is the most
common Spark mistake; see [Ignite configuration](./configuration.md).

> [!TIP]
> All defaults are `Enabled = true`. A user opts **out** of health checks or tracing by setting `Enabled`
> to `false` — ES.FX uses positive `Enabled` toggles, not `Disable*` flags.

## Step 4 — Write the hosting extension

`Hosting/{Service}HostingExtensions.cs` exposes the public entry point. Name it `Ignite{Service}...` (for
Redis, `IgniteRedisClient`) and extend `IHostApplicationBuilder` so it runs in **Phase A** — between
`builder.Ignite(...)` and `builder.Build()`.

### The canonical signature

Every registration method takes the same five parameters, in this order:

```csharp
public static void IgniteRedisClient(this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<RedisSparkSettings>? configureSettings = null,
    Action<RedisSparkOptions>? configureOptions = null,
    string configurationSectionPath = RedisSpark.ConfigurationSectionPath)
```

| Parameter | Purpose |
| --- | --- |
| `name` | Selects the config **sub-section** to read via `SparkConfig.Path(name, sectionPath)` → `Ignite:Redis:{name}`. Does not affect DI. |
| `serviceKey` | When non-null, registers the service as a **keyed** singleton and suffixes the health-check name with `[serviceKey]`. When null, registers the default (unkeyed) service. |
| `configureSettings` | Runs **after** settings are read from configuration, so it overrides `appsettings.json`. |
| `configureOptions` | Runs **after** options are read from configuration, so it overrides `appsettings.json`. |
| `configurationSectionPath` | Overrides the root section. Defaults to `{Service}Spark.ConfigurationSectionPath`. Most callers never set it. |

### The registration body

The method follows a fixed sequence. Study the reference and reuse the pattern verbatim:

```csharp
public static void IgniteRedisClient(this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<RedisSparkSettings>? configureSettings = null,
    Action<RedisSparkOptions>? configureOptions = null,
    string configurationSectionPath = RedisSpark.ConfigurationSectionPath)
{
    // 1. Guard against double-registration for this key
    builder.GuardConfigurationKey($"{RedisSpark.Name}[{serviceKey}]");

    // 2. Resolve the config path (root + optional name)
    var configPath = SparkConfig.Path(name, configurationSectionPath);

    // 3. Bind Settings from "{path}:Settings", run configureSettings, register keyed
    var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
    builder.Services.AddKeyedSingleton(serviceKey, settings);

    // 4. Bind Options from "{path}" directly, run configureOptions
    var optionsBuilder = builder.Services
        .AddOptions<RedisSparkOptions>(serviceKey ?? Options.DefaultName)
        .BindConfiguration(configPath);
    if (configureOptions is not null) optionsBuilder.Configure(configureOptions);

    // 5. Register the service (keyed by serviceKey; null = default)
    builder.Services.AddKeyedSingleton<IConnectionMultiplexer>(serviceKey, (sp, _) =>
    {
        var options = sp.GetRequiredService<IOptionsMonitor<RedisSparkOptions>>()
            .Get(serviceKey ?? Options.DefaultName);
        // … build the client from options, pass ILoggerFactory through …
    });

    // 6. Wire health checks + tracing from settings
    ConfigureObservability(builder, serviceKey, settings);
}
```

The load-bearing details:

- **Guard first.** `builder.GuardConfigurationKey($"{RedisSpark.Name}[{serviceKey}]")` records the key in
  `builder.Properties`. A second call for the same key throws `ReconfigurationNotSupportedException`.
- **`SparkConfig.Path(name, configurationSectionPath)`** produces the effective section: the root when
  `name` is null, or `{root}:{name}` when named.
- **`SparkConfig.Name(name, defaultName)`** normalizes a caller-supplied name — it trims the value and
  falls back to the (trimmed) `defaultName` when `name` is null, empty, or whitespace. Reach for it when
  you need a defaulted, trimmed Spark name (for example to derive a stable registration or health-check
  identifier):

  ```csharp
  var resolvedName = SparkConfig.Name(name, RedisSpark.Name);
  ```
- **`SparkConfig.GetSettings(...)`** binds Settings from `"{path}:Settings"` and then invokes
  `configureSettings` — that is why the delegate overrides `appsettings.json`. Register the resulting
  settings instance as a **keyed** singleton so the health check and factory can retrieve the exact
  per-instance settings later.
- **Options bind at the section directly** via `AddOptions<T>(serviceKey ?? Options.DefaultName)
  .BindConfiguration(configPath)`, keyed by the same name so `IOptionsMonitor<T>.Get(...)` resolves the
  right instance.
- **Register the service keyed by `serviceKey`.** Passing `null` makes it the default (unkeyed)
  registration; passing a key makes it resolvable with `[FromKeyedServices("…")]`.

> [!WARNING]
> Calling the same Spark twice for the same `serviceKey` throws `ReconfigurationNotSupportedException`.
> Always guard on `$"{Name}[{serviceKey}]"` as the very first line so duplicate registrations fail fast
> instead of silently double-wiring the service.

### Cross-cutting one-time setup

Some Sparks need process-wide setup that must run **exactly once**, regardless of how many named or keyed
instances are registered — for example a single global instrumentation options object. Use the guard's
check-then-set pair to do it once:

```csharp
const string configureOnceKey = $"{RedisSpark.Name}.Global.Tracing.Configure";
if (!builder.IsGuardConfigurationKeySet(configureOnceKey))
{
    builder.GuardConfigurationKey(configureOnceKey);
    // one-time global configuration here
}
```

`IsGuardConfigurationKeySet` reports whether the key was already registered; `GuardConfigurationKey`
claims it. Together they gate a block to a single execution across all registrations.

## Step 5 — Wire observability

A private helper (Redis calls it `ConfigureObservability`) reads the per-instance `settings` and wires
tracing and health checks conditionally:

- **Tracing** — when `settings.Tracing.Enabled`, add the OpenTelemetry `ActivitySource` for your client
  library to the tracer provider (`AddOpenTelemetry().WithTracing(t => t.AddSource(...))`). Gate it on the
  toggle so a user can turn it off with `Tracing.Enabled = false`.
- **Health checks** — when `settings.HealthChecks.Enabled`, register your `IHealthCheck` and honor the
  Settings knobs: `FailureStatus`, `Timeout`, and `Tags`.

```csharp
if (settings.HealthChecks.Enabled)
{
    var healthCheckName =
        $"{RedisSpark.Name}{(serviceKey is null ? string.Empty : $"[{serviceKey}]")}";

    builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(healthCheckName,
        sp => new SimpleRedisHealthCheck(serviceKey is null
            ? sp.GetRequiredService<IConnectionMultiplexer>()
            : sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey)),
        settings.HealthChecks.FailureStatus,
        [nameof(Redis), .. settings.HealthChecks.Tags],
        settings.HealthChecks.Timeout));
}
```

Two conventions to copy:

- **Name the check `{Name}` and suffix keyed instances with `[serviceKey]`** so multiple instances
  produce distinct, resolvable check names.
- **Compose tags as `[<default tag>, .. settings.HealthChecks.Tags]`** — a default identifying tag plus
  whatever the user supplied. Health-check tags decide liveness vs readiness: the **readiness** endpoint
  (`HealthChecksEndpoints.ReadinessEndpointName`, `"Readiness"`) aggregates all checks, while the
  **liveness** endpoint (`HealthChecksEndpoints.LivenessEndpointName`, `"Liveness"`) counts only checks
  tagged `"live"` (`HealthChecksTags.Live`). A Spark check is therefore **readiness-only** by default; a
  user opts a check into liveness by adding `"live"` to `Settings.HealthChecks.Tags`.

> [!TIP]
> Pass the app's `ILoggerFactory` into your client so its logs flow through the host's configured logging
> pipeline — Redis resolves `sp.GetService<ILoggerFactory>()` when building the connection. This keeps the
> Spark's logging consistent with the rest of the [Ignite](./index.md) stack.

## Step 6 — Implement the health check

Put the `IHealthCheck` in `HealthChecks/`. Keep it internal, resolve the client it verifies, and return
`HealthCheckResult.Healthy()` on success or the registration's `FailureStatus` on failure. Redis pings the
configured endpoints:

```csharp
internal sealed class SimpleRedisHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // … ping each configured endpoint …
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
```

Use `context.Registration.FailureStatus` (not a hard-coded status) so the `FailureStatus` from Settings is
honored. The check surfaces on the health endpoints that `app.Ignite()` maps — see
[Ignite configuration](./configuration.md).

## Post-build steps (optional)

Most Sparks do all their work in Phase A. A few also need a **Phase B** step that runs on the built `IHost`
after `app.Ignite()` — for example wiring middleware into a `WebApplication`. If yours does, add a second
extension named `app.Ignite{Service}()` on `IHost`, document that it must be called after `app.Ignite()`,
and note that web-only middleware runs only for `WebApplication` hosts. The [NSwag Spark](./sparks/nswag.md)
is an example (`app.IgniteNSwag()`).

## Authoring checklist

- [ ] `{Service}Spark` with `Name` and `ConfigurationSectionPath` (built from `IgniteConfigurationSections`).
- [ ] `{Service}SparkOptions` — plain service config, bound from the section directly.
- [ ] `{Service}SparkSettings` — `HealthChecks` + `Tracing` (+ `Metrics` if applicable), bound from `:Settings`.
- [ ] `Ignite{Service}...(IHostApplicationBuilder, name, serviceKey, configureSettings, configureOptions, configurationSectionPath)`.
- [ ] Guard first with `builder.GuardConfigurationKey($"{Name}[{serviceKey}]")`.
- [ ] Bind Settings via `SparkConfig.GetSettings`; bind Options via `AddOptions<T>().BindConfiguration`.
- [ ] Register the service keyed by `serviceKey`; run both delegates after binding.
- [ ] Health check named `{Name}` (`[serviceKey]` suffix), default tag + `Settings.HealthChecks.Tags`.
- [ ] Tracing/health checks gated on the `Enabled` toggles.
- [ ] Package auto-packs (`ES.FX.*` non-test projects do) — verify it lands in `.artifacts/nuget`.

## See also

- [Ignite overview](./index.md) — the two-phase lifecycle your Spark plugs into.
- [Ignite configuration](./configuration.md) — the `Ignite:` root, the `:Settings` sub-node, and readiness vs liveness.
- [Sparks catalog](./sparks/index.md) — the fixed anatomy and every built-in Spark.
- [Redis client integration](./sparks/stackexchange-redis.md) — the canonical Spark, documented for users.
- [Creating a new ES.FX library](../development/creating-a-library.md) — the packaging conventions for a new project.
