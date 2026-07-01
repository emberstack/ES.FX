---
title: Azure Common
description: Shared Azure client registration and observability plumbing that the Azure Sparks build on.
---

## Overview

Azure Common is the shared foundation the Azure Sparks are built on — it is **not** a client Spark you
register on its own. It provides two `IServiceCollection` extension methods that every Azure service Spark
([Blob Storage](./azure-storage-blobs.md), [Queue Storage](./azure-storage-queues.md),
[Table Storage](./azure-data-tables.md), [Key Vault Secrets](./azure-keyvault-secrets.md)) calls
internally to register an Azure SDK client and wire its observability:

- **`IgniteAzureClient<TClient, TOptions>`** — registers an Azure SDK client (e.g. `BlobServiceClient`)
  through [`Microsoft.Extensions.Azure`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.azure),
  binding the client's options from a configuration section and honoring keyed registration.
- **`IgniteAzureClientObservability<TClient>`** — adds the client's OpenTelemetry `ActivitySource` and a
  health check, driven by the standard Ignite `TracingSettings` / `HealthCheckSettings`.

The package brings in [`Azure.Identity`](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme)
and `Microsoft.Extensions.Azure`, so `DefaultAzureCredential` and the Azure client factory are available to
every Azure Spark without each package re-declaring them.

> [!NOTE]
> If you just want to use an Azure client in your app, reach for the service-specific Spark
> ([Blob Storage](./azure-storage-blobs.md), [Queue Storage](./azure-storage-queues.md),
> [Table Storage](./azure-data-tables.md), or [Key Vault Secrets](./azure-keyvault-secrets.md)) — each
> exposes a `builder.IgniteAzure...()` call with its own Settings and Options. Azure Common is the layer
> **underneath** those calls, documented here for authors building a new Azure Spark.

## Install the package

You rarely install Azure Common directly — it comes transitively with any Azure Spark. Install it
explicitly only when authoring a new Azure client integration:

```bash
dotnet add package ES.FX.Ignite.Azure.Common
```

```xml
<PackageReference Include="ES.FX.Ignite.Azure.Common" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register an Azure client

`IgniteAzureClient` registers the Azure SDK client type through the Azure client factory. The concrete
Azure Spark calls it with its client and options types, the resolved config section, and the
client-options delegate:

```csharp
public static void IgniteAzureClient<TClient, TOptions>(
    this IServiceCollection services,
    string? serviceKey,
    IConfigurationSection configuration,
    Action<TOptions>? configureOptions = null)
    where TOptions : class
    where TClient : class;
```

| Parameter | Purpose |
| --- | --- |
| `TClient` | The Azure SDK client type to register (e.g. `BlobServiceClient`, `SecretClient`). |
| `TOptions` | The matching Azure SDK client options type (e.g. `BlobClientOptions`). |
| `serviceKey` | When non-null and non-whitespace, the client is additionally registered as a **keyed** singleton under this key, and the underlying Azure client factory registration is given the same name. When null, empty, or whitespace, the key is treated as null and only the default (unkeyed) registration is made. |
| `configuration` | The `IConfigurationSection` the client options bind from — the Spark passes its resolved `Ignite:{Service}[:{name}]` section. |
| `configureOptions` | Optional delegate invoked **after** the options are bound from configuration, so it overrides the bound values. |

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `TClient` (via `AddAzureClients`) | Singleton | Built by the Azure client factory from the bound `TOptions`. |
| `TClient` (keyed) | Singleton | Added only when `serviceKey` is non-null and non-whitespace; resolves the factory's named client. |

Because registration goes through `Microsoft.Extensions.Azure`, the client also participates in that
library's shared credential and client-factory infrastructure — including `DefaultAzureCredential` from
`Azure.Identity`.

### How an Azure Spark uses it

This is the exact pattern the [Blob Storage Spark](./azure-storage-blobs.md) follows — the Spark resolves
its config section, binds its Settings, then delegates client registration and observability to Azure
Common:

```csharp
public static void IgniteAzureBlobServiceClient(this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<AzureBlobStorageSparkSettings>? configureSettings = null,
    Action<BlobClientOptions>? configureClientOptions = null,
    string configurationSectionPath = AzureBlobStorageSpark.ConfigurationSectionPath)
{
    builder.GuardConfigurationKey($"{AzureBlobStorageSpark.Name}-[{serviceKey}]");

    var configPath = SparkConfig.Path(name, configurationSectionPath);
    var settings = SparkConfig.GetSettings(builder.Configuration, configPath, configureSettings);
    builder.Services.AddKeyedSingleton(serviceKey, settings);

    builder.Services.IgniteAzureClient<BlobServiceClient, BlobClientOptions>(
        serviceKey,
        builder.Configuration.GetSection(configPath),
        configureClientOptions);

    builder.Services.IgniteAzureClientObservability<BlobServiceClient>(
        serviceKey,
        settings.Tracing,
        settings.HealthChecks,
        (_, client) => new SimpleBlobServiceHealthCheck(client));
}
```

> [!WARNING]
> Azure Common's helpers do **not** enforce the reconfiguration guard — that is the concrete Spark's job
> via `builder.GuardConfigurationKey(...)` (shown above), which throws `ReconfigurationNotSupportedException`
> when the same Spark is registered twice for the same key. If you author a new Azure Spark on top of these
> helpers, call the guard first.

### Keyed clients

Pass a non-null `serviceKey` to register the client as a keyed singleton in addition to the named factory
client. The Azure Spark surfaces its own `name`/`serviceKey` parameters and forwards `serviceKey` here, so
consumers resolve the client with `[FromKeyedServices("…")]`:

```csharp
// In the consuming app, via a service-specific Spark:
builder.IgniteAzureBlobServiceClient(name: "reports", serviceKey: "reports");
```

```csharp
public sealed class ReportStore([FromKeyedServices("reports")] BlobServiceClient client)
{
    // ...
}
```

`name` and `serviceKey` do different jobs: `name` selects the configuration sub-section
(`Ignite:{Service}:{name}`) the Spark reads, while `serviceKey` drives keyed DI. Azure Common only sees the
already-resolved `IConfigurationSection` and the `serviceKey`.

## Add client observability

`IgniteAzureClientObservability` wires the tracing source and health check for an already-registered Azure
client, driven by the same `TracingSettings` and `HealthCheckSettings` every Spark exposes:

```csharp
public static void IgniteAzureClientObservability<TClient>(
    this IServiceCollection services,
    string? serviceKey,
    TracingSettings tracingSettings,
    HealthCheckSettings healthCheckSettings,
    Func<IServiceProvider, TClient, IHealthCheck> healthCheckFactory)
    where TClient : class;
```

| Parameter | Purpose |
| --- | --- |
| `TClient` | The registered Azure client type the observability applies to. |
| `serviceKey` | The keyed-service key used to resolve the client for the health check, and to suffix the health-check name. Null, empty, or whitespace values are treated as null — the health check then resolves the default (unkeyed) client and the name carries no suffix. |
| `tracingSettings` | Standard Ignite tracing toggle. When `Enabled`, adds the client's tracing source. |
| `healthCheckSettings` | Standard Ignite health-check settings (`Enabled`, `FailureStatus`, `Timeout`, `Tags`). |
| `healthCheckFactory` | A factory that builds the `IHealthCheck` from the resolved service provider and the keyed client instance. Called only when `HealthChecks.Enabled` is `true`. |

The `TracingSettings` and `HealthCheckSettings` types come from `ES.FX.Ignite.Spark`; the Azure service
Sparks expose them through their `configureSettings` delegate as `settings.Tracing` and
`settings.HealthChecks`.

## Configuration

Azure Common has **no configuration section of its own** and defines no `SparkOptions` or `SparkSettings`
types. Each Azure Spark owns its section under the `Ignite:` root (for example `Ignite:AzureBlobStorage`),
binds its client options from that section, and passes the resolved `IConfigurationSection` down to
`IgniteAzureClient`. See the individual Azure Spark pages for their configuration schema and the
Settings-vs-Options split:

- [Azure Blob Storage](./azure-storage-blobs.md)
- [Azure Queue Storage](./azure-storage-queues.md)
- [Azure Table Storage](./azure-data-tables.md)
- [Azure Key Vault Secrets](./azure-keyvault-secrets.md)

The `configureOptions` delegate passed through `IgniteAzureClient` runs **after** the client options are
bound from configuration, so a delegate always overrides the bound `appsettings.json` values.

## Health checks

When `HealthCheckSettings.Enabled` is `true`, `IgniteAzureClientObservability` registers one health check
per client. The name is built from the client type:

```text
Azure-{TClient.Name}
```

For a keyed registration the key is appended — for example `Azure-BlobServiceClient-[reports]`. The check
is tagged `Azure` and `{TClient.Name}` (e.g. `BlobServiceClient`), plus any tags supplied via
`HealthCheckSettings.Tags`. `HealthCheckSettings.FailureStatus` and `HealthCheckSettings.Timeout` are
applied to the registration.

The actual check logic is provided by the concrete Spark through the `healthCheckFactory` (e.g. the Blob
Spark's `SimpleBlobServiceHealthCheck`). Health checks surface at the health endpoint mapped by
`app.Ignite()`.

Disable the check by turning off `HealthChecks.Enabled` in the owning Spark's settings — for example:

```csharp
builder.IgniteAzureBlobServiceClient(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

When `TracingSettings.Enabled` is `true`, `IgniteAzureClientObservability` adds an OpenTelemetry
`ActivitySource` matching the client's namespace with a wildcard:

```text
{TClient.Namespace}.*
```

For a `BlobServiceClient` (namespace `Azure.Storage.Blobs`) this registers the `Azure.Storage.Blobs.*`
source pattern, so the Azure SDK's own activities appear as spans in the Ignite OpenTelemetry pipeline.
Disable it by turning off `Tracing.Enabled` in the owning Spark's settings:

```csharp
builder.IgniteAzureBlobServiceClient(configureSettings: settings =>
    settings.Tracing.Enabled = false);
```

> [!TIP]
> To ship the spans somewhere, configure an OpenTelemetry exporter through Ignite — for example the
> [Seq exporter Spark](./seq-exporter.md).

### Logging

Azure Common does not wire logging itself. Clients registered through `Microsoft.Extensions.Azure`
integrate with the app's `ILoggerFactory` automatically, so Azure SDK logs flow through the same logging
pipeline as the rest of your app — including [Serilog](./serilog.md) when you enable it.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Azure Blob Storage Spark](./azure-storage-blobs.md)
- [Azure Queue Storage Spark](./azure-storage-queues.md)
- [Azure Table Storage Spark](./azure-data-tables.md)
- [Azure Key Vault Secrets Spark](./azure-keyvault-secrets.md)
- [Creating a Spark](../creating-a-spark.md)
- [Microsoft.Extensions.Azure documentation](https://learn.microsoft.com/dotnet/api/microsoft.extensions.azure)
