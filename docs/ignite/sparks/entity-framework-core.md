---
title: Entity Framework Core integration
description: Register an EF Core DbContext against SQL Server with Ignite, including retries, a health check, and OpenTelemetry tracing.
---

## Overview

The Entity Framework Core Spark registers your [EF Core](https://learn.microsoft.com/ef/core/) `DbContext`
against SQL Server into dependency injection, with connection retries, a health check, and OpenTelemetry
tracing already wired up. Call `builder.IgniteSqlServerDbContext<TDbContext>()` once and inject
`TDbContext` (or an `IDbContextFactory<TDbContext>`) anywhere in your app — the connection string binding,
transient retry handling, the `/health` probe, and SQL command traces come for free.

Under the hood the Spark:

- Binds a `SqlServerDbContextSparkOptions<TDbContext>` (connection details) and a
  `SqlServerDbContextSparkSettings<TDbContext>` (observability toggles) from the `Ignite:DbContext`
  configuration section.
- Registers `TDbContext` (via `AddDbContext`, or `AddDbContextFactory` for the factory variant) configured
  to use SQL Server, with `EnableRetryOnFailure` on by default and an optional command timeout.
- Wraps the provider with [EntityFramework.Exceptions](https://github.com/Giorgi/EntityFramework.Exceptions)
  (`UseExceptionProcessor`) so provider-specific database errors surface as typed exceptions.
- Adds an EF Core `DbContext` health check.
- Adds `SqlClient` OpenTelemetry instrumentation so SQL commands appear in your traces.

> [!TIP]
> Prefer the raw EF Core helpers without Ignite's DI, config binding, and observability wiring? See
> [Entity Framework Core additions](../../additions/entity-framework-core.md).

> [!NOTE]
> This Spark targets **SQL Server**. The connection is configured with `UseSqlServer`; the base
> `ES.FX.Ignite.Microsoft.EntityFrameworkCore` package supplies the provider-agnostic pieces (the Spark
> definition, the migrations task, and the migrations health check) that this SQL Server package builds on.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer
```

```xml
<PackageReference Include="ES.FX.Ignite.Microsoft.EntityFrameworkCore.SqlServer" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

The SQL Server package references the base `ES.FX.Ignite.Microsoft.EntityFrameworkCore` package
transitively, so you only add the one package above.

## Register the client

Call `IgniteSqlServerDbContext<TDbContext>` on your host application builder, after `builder.Ignite()`,
passing your own `DbContext` type:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteSqlServerDbContext<CatalogDbContext>();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteSqlServerDbContext<TDbContext>(
    this IHostApplicationBuilder builder,
    string? name = null,
    Action<SqlServerDbContextSparkSettings<TDbContext>>? configureSettings = null,
    Action<SqlServerDbContextSparkOptions<TDbContext>>? configureOptions = null,
    Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContextOptionsBuilder = null,
    Action<SqlServerDbContextOptionsBuilder>? configureSqlServerDbContextOptionsBuilder = null,
    ServiceLifetime lifetime = ServiceLifetime.Transient,
    string configurationSectionPath = DbContextSpark.ConfigurationSectionPath)
    where TDbContext : DbContext;
```

The two extra `configure…` delegates give you full control of the underlying builders when the bound
options are not enough:

- `configureDbContextOptionsBuilder` — customize the EF Core `DbContextOptionsBuilder` (e.g. add
  interceptors, enable sensitive-data logging).
- `configureSqlServerDbContextOptionsBuilder` — customize the SQL Server provider builder (e.g. migrations
  assembly, extra `SqlServerDbContextOptionsBuilder` tuning).

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `TDbContext` | `Transient` (configurable via `lifetime`) | Your context, configured for SQL Server with retries and the exception processor. |
| `SqlServerDbContextSparkSettings<TDbContext>` | Singleton | The resolved observability settings. |
| `IOptionsMonitor<SqlServerDbContextSparkOptions<TDbContext>>` | Singleton | The bound connection options. |
| Health check `DbContext.{name}` | — | Verifies the context can reach the database. See [Health checks](#health-checks). |
| SQL Client OpenTelemetry instrumentation | — | SQL commands appear as spans. See [Tracing](#tracing). |

> [!NOTE]
> This Spark keys instances by the **`TDbContext` type**, not by a `serviceKey`. `TDbContext` is registered
> as its own concrete type in DI, so there is no keyed-service parameter on the registration call.

### Consume the context

Inject `TDbContext` directly:

```csharp
public sealed class CatalogService(CatalogDbContext dbContext)
{
    public Task<Product?> FindAsync(int id) =>
        dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);
}
```

> [!WARNING]
> Registering the same `TDbContext` twice throws `ReconfigurationNotSupportedException` (the guard key is
> `DbContext[{TDbContext full name}]`). Register each context type exactly once. To wire multiple contexts,
> call `IgniteSqlServerDbContext<T>()` once per distinct `DbContext` type.

### Register multiple contexts

Because instances are keyed by type, register each context with its own generic argument, and give each a
`name` so it reads its own configuration sub-section:

```csharp
builder.IgniteSqlServerDbContext<CatalogDbContext>(name: "Catalog");
builder.IgniteSqlServerDbContext<OrdersDbContext>(name: "Orders");
```

`name` selects the configuration sub-section. When omitted it defaults to the **`TDbContext` type name**
(e.g. `CatalogDbContext`), so config is read from `Ignite:DbContext:CatalogDbContext`. Passing
`name: "Catalog"` reads from `Ignite:DbContext:Catalog` instead. It does not affect DI — the context is
always resolved by its type.

### Register a context factory

When a component needs to create short-lived contexts on demand (background work, parallel operations), use
the factory variant. It registers an `IDbContextFactory<TDbContext>` with the given `lifetime` and also
registers `TDbContext` itself as `Scoped` — unless `lifetime` is `Transient`, in which case the context is
registered as `Transient` too. The signature mirrors `IgniteSqlServerDbContext<TDbContext>`:

```csharp
builder.IgniteSqlServerDbContextFactory<CatalogDbContext>();
```

```csharp
public sealed class Importer(IDbContextFactory<CatalogDbContext> factory)
{
    public async Task ImportAsync()
    {
        await using var dbContext = await factory.CreateDbContextAsync();
        // ...
    }
}
```

## Configuration

All configuration lives under the `Ignite:DbContext` section, sub-keyed by `name` (defaulting to the
`TDbContext` type name). Both delegates (`configureSettings`, `configureOptions`) run **after**
configuration is read from `appsettings.json`, so a delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into two types with two separate delegates:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Options** | `SqlServerDbContextSparkOptions<TDbContext>` | The SQL Server connection itself. | `Ignite:DbContext:{name}` | `configureOptions` |
| **Settings** | `SqlServerDbContextSparkSettings<TDbContext>` | Ignite observability toggles. | `Ignite:DbContext:{name}:Settings` | `configureSettings` |

`SqlServerDbContextSparkOptions<TDbContext>` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `ConnectionString` | `string?` | none | The SQL Server connection string to connect to. |
| `DisableRetry` | `bool` | `false` | When `false`, `EnableRetryOnFailure` is applied for transient-fault resilience. Set `true` to turn retries off. |
| `CommandTimeout` | `int?` | none | Command timeout in seconds. When set, applied to the provider via `CommandTimeout`. |

`SqlServerDbContextSparkSettings<TDbContext>` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the `DbContext` health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `null` (treated as `Unhealthy`) | Reported status when the check fails. Leave unset to report `Unhealthy`. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout for the health check. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `DbContext` tag. |
| `Tracing.Enabled` | `bool` | `true` | Adds SQL Client tracing instrumentation. |

> [!NOTE]
> This Spark exposes no `Metrics` setting — its SQL Server instrumentation contributes tracing only.

### Configure via appsettings

`ConnectionString`, `DisableRetry`, and `CommandTimeout` sit at the section root (under the context's
sub-key); the observability toggles nest under a `Settings` sub-section:

```json
{
  "Ignite": {
    "DbContext": {
      "CatalogDbContext": {
        "ConnectionString": "Server=localhost;Database=Catalog;Trusted_Connection=True;TrustServerCertificate=True",
        "CommandTimeout": 30,
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
}
```

> [!IMPORTANT]
> The sub-key matches the `name` argument — which defaults to the `TDbContext` type name. If you pass
> `name: "Catalog"`, move the block under `Ignite:DbContext:Catalog` to match.

### Configure with delegates

`configureSettings` and `configureOptions` are separate delegates. Both run after `appsettings.json`, so
values set here override the JSON above:

```csharp
builder.IgniteSqlServerDbContext<CatalogDbContext>(
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
        settings.Tracing.Enabled = true;
    },
    configureOptions: options =>
    {
        options.ConnectionString = "Server=localhost;Database=Catalog;Trusted_Connection=True;TrustServerCertificate=True";
        options.CommandTimeout = 30;
    });
```

For finer control over EF Core or the SQL Server provider than the bound options offer, use the builder
delegates — for example to set the migrations assembly:

```csharp
builder.IgniteSqlServerDbContext<CatalogDbContext>(
    configureSqlServerDbContextOptionsBuilder: sqlOptions =>
        sqlOptions.MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName),
    configureDbContextOptionsBuilder: (sp, dbOptions) =>
        dbOptions.EnableSensitiveDataLogging());
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`DbContextSpark.ConfigurationSectionPath` (`"Ignite:DbContext"`). Most apps never change it; supply a
custom path only if you want the DbContext config to live somewhere other than `Ignite:DbContext`.

## Health checks

When `HealthChecks.Enabled` is `true` (the default), the Spark registers an EF Core `DbContext` health
check named **`DbContext.{name}`** — for example `DbContext.CatalogDbContext`. The check verifies that the
context can connect to and query the configured database.

The check is tagged `DbContext`, plus any tags you add via `HealthChecks.Tags`, and reports
`HealthChecks.FailureStatus` (unset by default, which reports `Unhealthy`) when it fails. It surfaces at the
health endpoint mapped by `app.Ignite()`.

Disable it via configuration:

```json
{
  "Ignite": {
    "DbContext": {
      "CatalogDbContext": {
        "Settings": {
          "HealthChecks": { "Enabled": false }
        }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteSqlServerDbContext<CatalogDbContext>(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

> [!NOTE]
> The base package also ships a `RelationalDbContextMigrationsHealthCheck<TContext>` that reports the
> registration's failure status (`Unhealthy` by default) when the database has pending migrations. It is not
> registered by this Spark; add it to your own health checks (`AddHealthChecks().AddCheck<…>()`) if you want
> a pending-migrations probe.

## Applying migrations

The base `ES.FX.Ignite.Microsoft.EntityFrameworkCore` package integrates with the
[Migrations Spark](./migrations.md). Register a migrations task for your context and the migration runner
applies it at startup:

```csharp
builder.IgniteSqlServerDbContext<CatalogDbContext>();
builder.AddDbContextMigrationsTask<CatalogDbContext>();
builder.IgniteMigrationsService();
```

`AddDbContextMigrationsTask<TDbContext>()` registers a `RelationalDbContextMigrationsTask<TDbContext>` as an
`IMigrationsTask`; `IgniteMigrationsService()` runs every registered task once during startup. See
[Migrations](../../libraries/migrations.md) for the full pattern.

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark adds SQL Client instrumentation
(`AddSqlClientInstrumentation`) to the Ignite OpenTelemetry pipeline, so SQL commands issued through the
context appear as spans in your traces.

Disable it via configuration:

```json
{
  "Ignite": {
    "DbContext": {
      "CatalogDbContext": {
        "Settings": {
          "Tracing": { "Enabled": false }
        }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteSqlServerDbContext<CatalogDbContext>(configureSettings: settings =>
    settings.Tracing.Enabled = false);
```

> [!TIP]
> To ship the spans somewhere, configure an OpenTelemetry exporter through Ignite — for example the
> [Seq exporter Spark](./seq-exporter.md).

### Logging

EF Core logs (queries, connections, transactions) flow through the app's configured logging pipeline — the
same one Ignite sets up — including [Serilog](./serilog.md) when you enable it, with no extra wiring. Use
`configureDbContextOptionsBuilder` if you want to adjust EF Core's own logging behavior (for example
`EnableSensitiveDataLogging()` in Development).

## See also

- [Entity Framework Core additions](../../additions/entity-framework-core.md)
- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Migrations](../../libraries/migrations.md)
- [SQL Server client integration](./microsoft-data-sqlclient.md)
- [EF Core documentation](https://learn.microsoft.com/ef/core/)
