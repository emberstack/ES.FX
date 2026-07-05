---
title: Sparks
description: Catalog of Ignite Sparks — self-contained integrations that wire a service into Ignite with DI, health checks, and OpenTelemetry.
---

## What is a Spark

A **Spark** is a self-contained integration that plugs a service into [Ignite](../index.md). One
`builder.Ignite{Service}...()` call binds the service's configuration, registers it in dependency
injection (keyed-capable), adds a health check, and wires OpenTelemetry — so you get a working,
observable client without hand-assembling any of it.

Every Spark reads its configuration from the `Ignite:` root, so it composes with the rest of Ignite and
with other Sparks out of the box. Add only the Sparks you need; each is its own `ES.FX.Ignite.{Provider}`
package.

## The Spark shape

Every Spark package follows the same fixed anatomy. Once you know one, you know them all (the canonical
reference is the [Redis Spark](./stackexchange-redis.md)):

| File | Role |
| --- | --- |
| `{Service}Spark.cs` | The Spark definition — holds `Name` and `ConfigurationSectionPath` constants. |
| `Configuration/{Service}SparkOptions.cs` | **Options** — the underlying service configuration (connection strings, native option objects). |
| `Configuration/{Service}SparkSettings.cs` | **Settings** — Ignite observability toggles (`HealthChecks`, `Tracing`, `Metrics`). |
| `Hosting/{Service}HostingExtensions.cs` | The public entry point(s), named `Ignite{Service}...`. |
| `HealthChecks/` | The health check(s), where the service supports one. |

> [!IMPORTANT]
> **Settings** and **Options** are different things bound from different places. Options bind the section
> directly (`Ignite:Redis:ConnectionString`); Settings bind a `:Settings` sub-node
> (`Ignite:Redis:Settings:HealthChecks:Enabled`). See the [configuration model](../configuration.md) for
> the full split.

## Anatomy of a registration call

Most Sparks share the same registration signature on `IHostApplicationBuilder`. Using Redis as the
example:

```csharp
public static void IgniteRedisClient(
    this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<RedisSparkSettings>? configureSettings = null,
    Action<RedisSparkOptions>? configureOptions = null,
    string configurationSectionPath = RedisSpark.ConfigurationSectionPath);
```

- **`name`** selects the configuration sub-section (`Ignite:Redis:cache`). It does not affect DI.
- **`serviceKey`** registers the service as a **keyed** singleton, resolved with `[FromKeyedServices("…")]`.
- **`configureSettings`** / **`configureOptions`** are separate delegates that run *after* `appsettings.json`
  and override it.
- **`configurationSectionPath`** overrides the root section; most apps leave it at the default.

Sparks run in Ignite's **pre-build** phase — between `builder.Ignite()` and `builder.Build()`. A few add a
**post-build** step you call after `app.Ignite()` (for example `app.IgniteNSwag()`); those pages call it
out.

> [!WARNING]
> Registering the same Spark twice for the same key throws `ReconfigurationNotSupportedException`. Register
> each service once; to register multiple instances, give each a distinct `serviceKey`.

## Catalog

Every Spark, grouped by function.

### Caching

| Spark | Purpose |
| --- | --- |
| [Redis](./stackexchange-redis.md) | Registers a StackExchange.Redis `IConnectionMultiplexer` with health checks and tracing. |

### Databases & data

| Spark | Purpose |
| --- | --- |
| [SQL Server client](./microsoft-data-sqlclient.md) | Registers a `Microsoft.Data.SqlClient` data source / `SqlConnection` factory. |
| [Entity Framework Core](./entity-framework-core.md) | Registers a `DbContext` (with the SQL Server provider) wired into Ignite. |
| [Migrations](./migrations.md) | The DI-driven migration runner (`MigrationsService`) that applies registered migration tasks at startup. |

### Azure

| Spark | Purpose |
| --- | --- |
| [Azure Common](./azure-common.md) | Shared Azure client and credential plumbing that the other Azure Sparks build on. |
| [Azure Table Storage](./azure-data-tables.md) | Registers a `TableServiceClient` for Azure Table Storage. |
| [Azure Key Vault Secrets](./azure-keyvault-secrets.md) | Registers a `SecretClient` for reading secrets from Azure Key Vault. |
| [Azure Blob Storage](./azure-storage-blobs.md) | Registers a `BlobServiceClient` for Azure Blob Storage. |
| [Azure Queue Storage](./azure-storage-queues.md) | Registers a `QueueServiceClient` for Azure Queue Storage. |

### External services

| Spark | Purpose |
| --- | --- |
| [Hermes Agent](./hermes-agent.md) | Registers the [ES.FX.NousResearch.HermesAgent](../../libraries/hermes-agent-client.md) typed `IHermesAgentClient` (static bearer key) with a live health check and tracing. |
| [Zendesk](./zendesk.md) | Registers the [ES.FX.Zendesk](../../libraries/zendesk-client.md) Kiota-generated `ZendeskSupportApiClient` and `ZendeskHelpCenterApiClient` (OAuth `client_credentials`) with a live health check and tracing. |

### API & documentation

| Spark | Purpose |
| --- | --- |
| [API versioning](./asp-versioning.md) | Wires `Asp.Versioning` into Ignite for versioned API endpoints. |
| [NSwag](./nswag.md) | Generates OpenAPI documents and UI via NSwag (adds a post-build `app.IgniteNSwag()` step). |
| [Swashbuckle](./swashbuckle.md) | Generates OpenAPI documents and Swagger UI via Swashbuckle. |

### Observability

| Spark | Purpose |
| --- | --- |
| [Serilog](./serilog.md) | Wires Serilog into Ignite as the logging pipeline. |
| [Seq OpenTelemetry exporter](./seq-exporter.md) | Ships OpenTelemetry logs and traces to a Seq server. |

### Validation & infrastructure

| Spark | Purpose |
| --- | --- |
| [FluentValidation](./fluentvalidation.md) | Registers FluentValidation validators and wires them into Ignite. |
| [Kubernetes client](./kubernetesclient.md) | Registers an `IKubernetes` client for talking to the Kubernetes API. |

## See also

- [Ignite overview](../index.md)
- [Ignite configuration model](../configuration.md)
- [Creating a Spark](../creating-a-spark.md)
- [Redis Spark](./stackexchange-redis.md) — the canonical example
