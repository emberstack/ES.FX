---
title: SQL Server client integration
description: Register a Microsoft.Data.SqlClient SqlConnection with Ignite, including a health check and OpenTelemetry tracing.
---

## Overview

The SQL Server client Spark registers a [Microsoft.Data.SqlClient](https://learn.microsoft.com/sql/connect/ado-net/microsoft-ado-net-sql-server)
`SqlConnection` into dependency injection, with a health check and OpenTelemetry tracing already wired up.
Call `builder.IgniteSqlServerClient("…")` once and inject a `SqlConnection` anywhere in your app — the
connection string, the `/health` probe, and distributed traces come for free.

Under the hood the Spark:

- Binds a `SqlServerClientSparkOptions` (the connection string) and a `SqlServerClientSparkSettings`
  (observability toggles) from the `Ignite:SqlServerClient` configuration section.
- Registers a `SqlConnection` built from those options. The lifetime is **transient** by default so each
  resolution yields a fresh, unopened connection.
- Optionally registers an `ISqlConnectionFactory` (via `IgniteSqlServerClientFactory`) when you prefer to
  create connections on demand rather than inject them directly.
- Adds a health check that opens a connection and runs a `SELECT 1;` probe.
- Adds the `Microsoft.Data.SqlClient` OpenTelemetry instrumentation so SQL commands appear in your traces.

> [!TIP]
> Need only the raw helpers — the `ISqlConnectionFactory` abstraction, the delegate factory, or the safe
> query wrapper — without Ignite's DI and observability wiring? See the
> [Microsoft.Data.SqlClient additions](../../additions/microsoft-data-sqlclient.md).

> [!NOTE]
> This page documents the **client** integration. ES.FX has no AppHost or orchestration layer — you point
> the Spark at a SQL Server instance you run yourself (locally, in a container, or a managed service) via
> the connection string.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Microsoft.Data.SqlClient
```

```xml
<PackageReference Include="ES.FX.Ignite.Microsoft.Data.SqlClient" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteSqlServerClient` on your host application builder, after `builder.Ignite()`. Unlike most
Sparks, `name` is **required** — it selects the configuration sub-section to read (pass an empty string
`""` to read the root `Ignite:SqlServerClient` section):

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteSqlServerClient("");

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteSqlServerClient(
    this IHostApplicationBuilder builder,
    string name,
    string? serviceKey = null,
    Action<SqlServerClientSparkSettings>? configureSettings = null,
    Action<SqlServerClientSparkOptions>? configureOptions = null,
    ServiceLifetime lifetime = ServiceLifetime.Transient,
    string configurationSectionPath = SqlServerClientSpark.ConfigurationSectionPath);
```

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `SqlConnection` | Transient (configurable via `lifetime`) | A new, unopened connection built from the connection string. Keyed when `serviceKey` is set. |
| `SqlServerClientSparkSettings` | Singleton | The resolved observability settings (keyed by `serviceKey`). |
| Health check `SqlServerClient` | — | Opens a connection and runs `SELECT 1;`. See [Health checks](#health-checks). |
| OpenTelemetry instrumentation | — | `Microsoft.Data.SqlClient` tracing. See [Tracing](#tracing). |

> [!IMPORTANT]
> The registered `SqlConnection` is not opened for you. Open it (`OpenAsync`) and dispose it per use — the
> transient lifetime means each injection is a distinct connection object. For pooled reuse, keep the
> default transient lifetime and rely on ADO.NET connection pooling (enabled by the connection string).

### Consume the client

Inject `SqlConnection` and open it where you need it:

```csharp
public sealed class ProductRepository(SqlConnection connection)
{
    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Products;";
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
```

> [!WARNING]
> Calling `IgniteSqlServerClient` (or `IgniteSqlServerClientFactory`) twice with the **same** `serviceKey`
> throws `ReconfigurationNotSupportedException`. Register each connection exactly once. To register more
> than one SQL Server connection, give each a distinct `serviceKey` (see below).

### Register with a connection factory

When you want to create connections on demand — for example one per unit of work — use
`IgniteSqlServerClientFactory` instead. It registers an `ISqlConnectionFactory` (and, for convenience, the
`SqlConnection` as well) so you can call `CreateConnection()` / `CreateConnectionAsync()`:

```csharp
builder.IgniteSqlServerClientFactory("");
```

```csharp
public sealed class OrderService(ISqlConnectionFactory connectionFactory)
{
    public async Task<int> PlaceOrderAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);
        // ...
        return 0;
    }
}
```

`ISqlConnectionFactory` and its `DelegateSqlConnectionFactory` implementation come from the
[Microsoft.Data.SqlClient additions](../../additions/microsoft-data-sqlclient.md) package, which this Spark
depends on.

### Register keyed clients

To connect to more than one SQL Server, register each as a **keyed** service with a distinct `serviceKey`,
and pass a matching `name` so each reads its own configuration sub-section:

```csharp
builder.IgniteSqlServerClient(name: "orders", serviceKey: "orders");
builder.IgniteSqlServerClient(name: "reporting", serviceKey: "reporting");
```

`name` and `serviceKey` do different jobs:

- **`name`** selects the configuration sub-section. `name: "orders"` reads from
  `Ignite:SqlServerClient:orders` instead of `Ignite:SqlServerClient`. It does not affect DI.
- **`serviceKey`** registers `SqlConnection` (and `ISqlConnectionFactory`, for the factory overload) as a
  **keyed** service. Resolve it with `[FromKeyedServices("…")]`. When `serviceKey` is `null`, the
  connection is the default (unkeyed) registration.

The matching configuration:

```json
{
  "Ignite": {
    "SqlServerClient": {
      "orders": {
        "ConnectionString": "Server=db;Database=Orders;Trusted_Connection=True;Encrypt=True"
      },
      "reporting": {
        "ConnectionString": "Server=db;Database=Reporting;Trusted_Connection=True;Encrypt=True"
      }
    }
  }
}
```

Resolve the keyed connections by key:

```csharp
public sealed class ReportGenerator(
    [FromKeyedServices("orders")] SqlConnection orders,
    [FromKeyedServices("reporting")] SqlConnection reporting)
{
    // ...
}
```

## Configuration

All SQL Server client configuration lives under the `Ignite:SqlServerClient` section. Both delegates
(`configureSettings`, `configureOptions`) run **after** configuration is read from `appsettings.json`, so a
delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into two types with two separate delegates:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Options** | `SqlServerClientSparkOptions` | The SQL Server connection itself. | `Ignite:SqlServerClient` | `configureOptions` |
| **Settings** | `SqlServerClientSparkSettings` | Ignite observability toggles. | `Ignite:SqlServerClient:Settings` | `configureSettings` |

`SqlServerClientSparkOptions` members:

| Member | Type | Purpose |
| --- | --- | --- |
| `ConnectionString` | `string?` | The [Microsoft.Data.SqlClient connection string](https://learn.microsoft.com/sql/connect/ado-net/connection-string-syntax) used to build every `SqlConnection`. |

`SqlServerClientSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the SQL Server health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `SqlServerClient` tag. |
| `Tracing.Enabled` | `bool` | `true` | Adds the `Microsoft.Data.SqlClient` tracing instrumentation. |

> [!NOTE]
> The SQL Server client Spark exposes no `Metrics` setting — `Microsoft.Data.SqlClient` instrumentation
> here contributes tracing only.

### Configure via appsettings

`ConnectionString` sits at the section root; the observability toggles nest under a `Settings`
sub-section:

```json
{
  "Ignite": {
    "SqlServerClient": {
      "ConnectionString": "Server=localhost;Database=App;Trusted_Connection=True;Encrypt=True",
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
builder.IgniteSqlServerClient("",
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
        settings.Tracing.Enabled = true;
    },
    configureOptions: options =>
    {
        options.ConnectionString = "Server=localhost;Database=App;Trusted_Connection=True;Encrypt=True";
    });
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`SqlServerClientSpark.ConfigurationSectionPath` (`"Ignite:SqlServerClient"`). Most apps never change it;
supply a custom path only if you want the SQL Server config to live somewhere other than
`Ignite:SqlServerClient`.

## Health checks

The Spark registers a health check named **`SqlServerClient`** by default (`HealthChecks.Enabled` is
`true`). For a keyed registration the name carries the key suffix — e.g. `SqlServerClient[orders]`. The
check opens a `SqlConnection` from the configured connection string and executes `SELECT 1;`; a failure
(connection or query) reports the configured `FailureStatus` (`Unhealthy` by default).

The check is tagged `SqlServerClient`, plus any tags you add via `HealthChecks.Tags`. It surfaces at the
health endpoint mapped by `app.Ignite()`.

Disable it via configuration:

```json
{
  "Ignite": {
    "SqlServerClient": {
      "Settings": {
        "HealthChecks": { "Enabled": false }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteSqlServerClient("", configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark calls
`AddSqlClientInstrumentation()` to add the `Microsoft.Data.SqlClient` instrumentation to the Ignite
OpenTelemetry pipeline, so SQL commands appear as spans in your traces.

Disable it via configuration:

```json
{
  "Ignite": {
    "SqlServerClient": {
      "Settings": {
        "Tracing": { "Enabled": false }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteSqlServerClient("", configureSettings: settings =>
    settings.Tracing.Enabled = false);
```

> [!NOTE]
> SqlClient instrumentation is **process-wide**: enabling tracing for any one client enables it for all
> `SqlConnection` activity in the process. Disabling `Tracing` on one keyed client has no effect if another
> registration (or anything else in the app) adds the instrumentation.

> [!TIP]
> To ship the spans somewhere, configure an OpenTelemetry exporter through Ignite — for example the
> [Seq exporter Spark](./seq-exporter.md).

### Logging

`Microsoft.Data.SqlClient` emits diagnostics through its own `EventSource`; ADO.NET events flow through the
app's configured logging and OpenTelemetry pipeline like the rest of your app — including
[Serilog](./serilog.md) when you enable it — with no extra wiring.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Entity Framework Core Spark](./entity-framework-core.md)
- [Microsoft.Data.SqlClient additions](../../additions/microsoft-data-sqlclient.md)
- [Serilog Spark](./serilog.md)
- [Microsoft.Data.SqlClient documentation](https://learn.microsoft.com/sql/connect/ado-net/microsoft-ado-net-sql-server)
