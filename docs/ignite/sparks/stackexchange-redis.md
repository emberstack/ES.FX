---
title: Redis client integration
description: Register a StackExchange.Redis IConnectionMultiplexer with Ignite, including health checks and OpenTelemetry tracing.
---

## Overview

The Redis Spark registers a shared [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/)
`IConnectionMultiplexer` into dependency injection, with a health check and OpenTelemetry tracing already
wired up. Call `builder.IgniteRedisClient()` once and inject `IConnectionMultiplexer` (or resolve
`IDatabase` from it) anywhere in your app — connection configuration, the `/health` probe, and distributed
traces come for free.

Under the hood the Spark:

- Binds a `RedisSparkOptions` (connection details) and a `RedisSparkSettings` (observability toggles) from
  the `Ignite:Redis` configuration section.
- Registers a singleton `IConnectionMultiplexer` built from those options, passing the app's
  `ILoggerFactory` to the client so Redis logs flow through your configured logging.
- Adds a health check that pings every configured endpoint.
- Adds the StackExchange.Redis OpenTelemetry `ActivitySource` so Redis commands appear in your traces.

> [!NOTE]
> This page documents the **client** integration. ES.FX has no AppHost or orchestration layer — you point
> the Spark at a Redis server you run yourself (locally, in a container, or a managed service) via the
> connection string.

> [!TIP]
> Just need raw `IDatabase` helpers (key-prefix lookup, batched key deletion) without Ignite? See the
> [StackExchange.Redis additions](../../additions/stackexchange-redis.md) package.

## Install the client

```bash
dotnet add package ES.FX.Ignite.StackExchange.Redis
```

```xml
<PackageReference Include="ES.FX.Ignite.StackExchange.Redis" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteRedisClient` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteRedisClient();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteRedisClient(
    this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<RedisSparkSettings>? configureSettings = null,
    Action<RedisSparkOptions>? configureOptions = null,
    string configurationSectionPath = RedisSpark.ConfigurationSectionPath);
```

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `IConnectionMultiplexer` | Singleton | The shared connection. Keyed when `serviceKey` is set. |
| `RedisSparkSettings` | Singleton | The resolved observability settings (keyed by `serviceKey`). |
| Health check `Redis` | — | Pings the connection. See [Health checks](#health-checks). |
| OpenTelemetry `ActivitySource` | — | `OpenTelemetry.Instrumentation.StackExchangeRedis`. See [Tracing](#tracing). |

### Consume the client

Inject `IConnectionMultiplexer` and get an `IDatabase` from it:

```csharp
public sealed class CacheService(IConnectionMultiplexer connection)
{
    private readonly IDatabase _database = connection.GetDatabase();

    public Task SetAsync(string key, string value) =>
        _database.StringSetAsync(key, value);

    public Task<RedisValue> GetAsync(string key) =>
        _database.StringGetAsync(key);
}
```

> [!WARNING]
> Calling `IgniteRedisClient` twice with the **same** `serviceKey` throws
> `ReconfigurationNotSupportedException`. Register each connection exactly once. To register more than one
> Redis connection, give each a distinct `serviceKey` (see below).

### Register keyed clients

To connect to more than one Redis server, register each as a **keyed** service with a distinct
`serviceKey`, and pass a matching `name` so each reads its own configuration sub-section:

```csharp
builder.IgniteRedisClient(name: "cache", serviceKey: "cache");
builder.IgniteRedisClient(name: "signalr", serviceKey: "signalr");
```

`name` and `serviceKey` do different jobs:

- **`name`** selects the configuration sub-section. `name: "cache"` reads from `Ignite:Redis:cache`
  instead of `Ignite:Redis`. It does not affect DI.
- **`serviceKey`** registers `IConnectionMultiplexer` as a **keyed** singleton. Resolve it with
  `[FromKeyedServices("…")]`. When `serviceKey` is `null`, the connection is the default (unkeyed)
  registration.

The matching configuration:

```json
{
  "Ignite": {
    "Redis": {
      "cache": {
        "ConnectionString": "cache.redis:6379"
      },
      "signalr": {
        "ConnectionString": "signalr.redis:6379"
      }
    }
  }
}
```

Resolve the keyed connections by key:

```csharp
public sealed class SessionStore(
    [FromKeyedServices("cache")] IConnectionMultiplexer cache,
    [FromKeyedServices("signalr")] IConnectionMultiplexer signalr)
{
    private readonly IDatabase _cache = cache.GetDatabase();
    // ...
}
```

## Configuration

All Redis configuration lives under the `Ignite:Redis` section. Both delegates
(`configureSettings`, `configureOptions`) run **after** configuration is read from `appsettings.json`, so
a delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into two types with two separate delegates:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Options** | `RedisSparkOptions` | The Redis connection itself. | `Ignite:Redis` | `configureOptions` |
| **Settings** | `RedisSparkSettings` | Ignite observability toggles. | `Ignite:Redis:Settings` | `configureSettings` |

`RedisSparkOptions` members:

| Member | Type | Purpose |
| --- | --- | --- |
| `ConnectionString` | `string?` | A StackExchange.Redis connection string, e.g. `localhost:6379`. Parsed via `ConfigurationOptions.Parse`. Takes precedence over `ConfigurationOptions` when set. |
| `ConfigurationOptions` | `ConfigurationOptions?` | A native StackExchange.Redis [`ConfigurationOptions`](https://stackexchange.github.io/StackExchange.Redis/Configuration) instance, for full control when a connection string is not enough. Used only when `ConnectionString` is null or whitespace — when `ConnectionString` is set, this is dropped wholesale, including custom TLS validation (`CertificateValidation`) and retry policies (`ReconnectRetryPolicy`). |

`RedisSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the Redis health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `Redis` tag. |
| `Tracing.Enabled` | `bool` | `true` | Adds the StackExchange.Redis tracing source. |

> [!NOTE]
> The Redis Spark exposes no `Metrics` setting — StackExchange.Redis instrumentation here contributes
> tracing only.

### Configure via appsettings

`ConnectionString` and `ConfigurationOptions` sit at the section root; the observability toggles nest under
a `Settings` sub-section:

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

### Configure with delegates

`configureSettings` and `configureOptions` are separate delegates. Both run after `appsettings.json`, so
values set here override the JSON above:

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

For full control over the connection — TLS, retries, keepalive — set `ConfigurationOptions` instead of a
connection string:

```csharp
builder.IgniteRedisClient(configureOptions: options =>
{
    options.ConfigurationOptions = new ConfigurationOptions
    {
        EndPoints = { "localhost:6379" },
        Ssl = true,
        AbortOnConnectFail = false
    };
});
```

> [!WARNING]
> When both are set, `ConnectionString` wins **wholesale** — the Spark parses it and ignores
> `ConfigurationOptions` entirely. This includes settings that cannot be expressed in a connection string,
> such as custom TLS certificate validation (`CertificateValidation`) and reconnect/retry policies
> (`ReconnectRetryPolicy`): any such customization on `ConfigurationOptions` is silently dropped. If you
> need those, set `ConfigurationOptions` and leave `ConnectionString` null or whitespace.

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`RedisSpark.ConfigurationSectionPath` (`"Ignite:Redis"`). Most apps never change it; supply a custom path
only if you want the Redis config to live somewhere other than `Ignite:Redis`.

## Health checks

The Spark registers a health check named **`Redis`** by default (`HealthChecks.Enabled` is `true`). For a
keyed registration the name carries the key suffix — e.g. `Redis[cache]`. The check iterates every
configured endpoint on the multiplexer: for standalone servers it pings the database and the server
endpoint; for cluster nodes it runs `CLUSTER INFO` and verifies `cluster_state:ok`.

The check is tagged `Redis`, plus any tags you add via `HealthChecks.Tags`. It surfaces at the health
endpoint mapped by `app.Ignite()`.

Disable it via configuration:

```json
{
  "Ignite": {
    "Redis": {
      "Settings": {
        "HealthChecks": { "Enabled": false }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteRedisClient(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark adds the
`OpenTelemetry.Instrumentation.StackExchangeRedis` `ActivitySource` to the Ignite OpenTelemetry pipeline
and attaches each registered connection to the instrumentation, so Redis commands appear as spans in your
traces.

Disable it via configuration:

```json
{
  "Ignite": {
    "Redis": {
      "Settings": {
        "Tracing": { "Enabled": false }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteRedisClient(configureSettings: settings =>
    settings.Tracing.Enabled = false);
```

> [!TIP]
> To ship the spans somewhere, configure an OpenTelemetry exporter through Ignite — for example the
> [Seq exporter Spark](./seq-exporter.md).

### Logging

The Spark passes the app's `ILoggerFactory` to StackExchange.Redis when it builds the connection (unless
your own `ConfigurationOptions` already set one). Redis client logs therefore flow through the same logging
pipeline as the rest of your app — including [Serilog](./serilog.md) when you enable it — with no extra
wiring.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [StackExchange.Redis additions](../../additions/stackexchange-redis.md) — raw `IDatabase` helpers with no Ignite dependency.
- [Serilog Spark](./serilog.md)
- [StackExchange.Redis documentation](https://stackexchange.github.io/StackExchange.Redis/)
- [StackExchange.Redis configuration](https://stackexchange.github.io/StackExchange.Redis/Configuration)
