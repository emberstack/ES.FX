---
title: Azure Table Storage client integration
description: Register an Azure.Data.Tables TableServiceClient with Ignite, including a health check and OpenTelemetry tracing.
---

## Overview

The Azure Table Storage Spark registers a shared
[Azure.Data.Tables](https://learn.microsoft.com/dotnet/api/overview/azure/data.tables-readme)
`TableServiceClient` into dependency injection, with a health check and OpenTelemetry tracing already
wired up. Call `builder.IgniteAzureTableServiceClient()` once and inject `TableServiceClient` (or a
`TableClient` obtained from it) anywhere in your app — connection configuration, the `/health` probe, and
distributed traces come for free.

Under the hood the Spark:

- Reads connection details (connection string or service URI + credential) from the
  `Ignite:Azure:Data:Tables` configuration section using the standard `Microsoft.Extensions.Azure` client
  factory, and binds an `AzureDataTablesSparkSettings` (observability toggles) from its `:Settings`
  sub-section.
- Registers a singleton `TableServiceClient` built from that configuration.
- Adds a health check that queries the Table service.
- Adds the `Azure.Data.Tables` OpenTelemetry `ActivitySource` so Table operations appear in your traces.

This Spark builds on the shared Azure client plumbing in the
[Azure Common](./azure-common.md) package.

> [!NOTE]
> This page documents the **client** integration. ES.FX has no AppHost or orchestration layer — you point
> the Spark at an Azure Storage / Table endpoint you provision yourself (Azure Storage, Azurite, or Cosmos
> DB Table API) via the connection string or service URI.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Azure.Data.Tables
```

```xml
<PackageReference Include="ES.FX.Ignite.Azure.Data.Tables" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteAzureTableServiceClient` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteAzureTableServiceClient();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteAzureTableServiceClient(
    this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<AzureDataTablesSparkSettings>? configureSettings = null,
    Action<TableClientOptions>? configureClientOptions = null,
    string configurationSectionPath = AzureDataTablesSpark.ConfigurationSectionPath);
```

> [!NOTE]
> The options delegate here is `configureClientOptions`, typed as the **native** Azure SDK
> `TableClientOptions` — not an ES.FX `SparkOptions` type. Connection details themselves (connection
> string, service URI, credential) are bound from configuration by the Azure client factory; use
> `configureClientOptions` for the SDK's own knobs such as retry policy and diagnostics.

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `TableServiceClient` | Singleton | The shared Table service client. Keyed when `serviceKey` is set. |
| `AzureDataTablesSparkSettings` | Singleton | The resolved observability settings (keyed by `serviceKey`). |
| Health check `Azure-TableServiceClient` | — | Queries the Table service. See [Health checks](#health-checks). |
| OpenTelemetry `ActivitySource` | — | `Azure.Data.Tables.*`. See [Tracing](#tracing). |

### Consume the client

Inject `TableServiceClient` and get a `TableClient` for a specific table:

```csharp
public sealed class OrdersStore(TableServiceClient tableService)
{
    private readonly TableClient _table = tableService.GetTableClient("orders");

    public async Task UpsertAsync(TableEntity entity, CancellationToken cancellationToken)
    {
        await _table.CreateIfNotExistsAsync(cancellationToken);
        await _table.UpsertEntityAsync(entity, cancellationToken: cancellationToken);
    }
}
```

> [!WARNING]
> Calling `IgniteAzureTableServiceClient` twice with the **same** `serviceKey` throws
> `ReconfigurationNotSupportedException`. Register each client exactly once. To register more than one
> Table service client, give each a distinct `serviceKey` (see below).

### Register keyed clients

To connect to more than one Table service endpoint, register each as a **keyed** service with a distinct
`serviceKey`, and pass a matching `name` so each reads its own configuration sub-section:

```csharp
builder.IgniteAzureTableServiceClient(name: "primary", serviceKey: "primary");
builder.IgniteAzureTableServiceClient(name: "audit", serviceKey: "audit");
```

`name` and `serviceKey` do different jobs:

- **`name`** selects the configuration sub-section. `name: "primary"` reads from
  `Ignite:Azure:Data:Tables:primary` instead of `Ignite:Azure:Data:Tables`. It does not affect DI.
- **`serviceKey`** registers `TableServiceClient` as a **keyed** singleton. Resolve it with
  `[FromKeyedServices("…")]`. When `serviceKey` is `null`, the client is the default (unkeyed)
  registration.

The matching configuration:

```json
{
  "Ignite": {
    "Azure": {
      "Data": {
        "Tables": {
          "primary": {
            "ServiceUri": "https://primary.table.core.windows.net"
          },
          "audit": {
            "ServiceUri": "https://audit.table.core.windows.net"
          }
        }
      }
    }
  }
}
```

Resolve the keyed clients by key:

```csharp
public sealed class AuditWriter(
    [FromKeyedServices("primary")] TableServiceClient primary,
    [FromKeyedServices("audit")] TableServiceClient audit)
{
    private readonly TableClient _audit = audit.GetTableClient("events");
    // ...
}
```

## Configuration

All Table Storage configuration lives under the `Ignite:Azure:Data:Tables` section. Both delegates
(`configureSettings`, `configureClientOptions`) run **after** configuration is read from
`appsettings.json`, so a delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into two parts with two separate delegates:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Client options** | `TableClientOptions` (Azure SDK) | The `TableServiceClient` itself: connection details plus SDK knobs (retry, diagnostics). | `Ignite:Azure:Data:Tables` | `configureClientOptions` |
| **Settings** | `AzureDataTablesSparkSettings` | Ignite observability toggles. | `Ignite:Azure:Data:Tables:Settings` | `configureSettings` |

Connection details are supplied through the section root and bound by the Azure client factory. Provide
**either** a full `ConnectionString`, **or** a `ServiceUri` (in which case a
[`DefaultAzureCredential`](https://learn.microsoft.com/dotnet/azure/sdk/authentication/credential-chains)
is used unless you configure another). See the
[Azure.Data.Tables configuration reference](https://learn.microsoft.com/dotnet/api/microsoft.extensions.azure)
for the full set of recognized keys.

`AzureDataTablesSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the Table service health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `Azure` and `TableServiceClient` tags. |
| `Tracing.Enabled` | `bool` | `true` | Adds the `Azure.Data.Tables` tracing source. |

> [!NOTE]
> This Spark exposes no `Metrics` setting — the Azure Table client contributes tracing only.

### Configure via appsettings

Connection details sit at the section root; the observability toggles nest under a `Settings`
sub-section:

```json
{
  "Ignite": {
    "Azure": {
      "Data": {
        "Tables": {
          "ServiceUri": "https://myaccount.table.core.windows.net",
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
}
```

To connect with a full connection string instead of a service URI + credential:

```json
{
  "Ignite": {
    "Azure": {
      "Data": {
        "Tables": {
          "ConnectionString": "UseDevelopmentStorage=true"
        }
      }
    }
  }
}
```

### Configure with delegates

`configureSettings` and `configureClientOptions` are separate delegates. Both run after
`appsettings.json`, so values set here override the JSON above:

```csharp
builder.IgniteAzureTableServiceClient(
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
        settings.Tracing.Enabled = true;
    },
    configureClientOptions: options =>
    {
        options.Retry.MaxRetries = 5;
        options.EnableTenantDiscovery = true;
    });
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`AzureDataTablesSpark.ConfigurationSectionPath` (`"Ignite:Azure:Data:Tables"`). Most apps never change it;
supply a custom path only if you want the Table config to live somewhere else.

## Health checks

The Spark registers a health check named **`Azure-TableServiceClient`** by default
(`HealthChecks.Enabled` is `true`). For a keyed registration the name carries the key suffix — e.g.
`Azure-TableServiceClient-[audit]`. The check runs a lightweight query against the Table service and
reports healthy when the call succeeds.

The check is tagged `Azure` and `TableServiceClient`, plus any tags you add via `HealthChecks.Tags`. It
surfaces at the health endpoint mapped by `app.Ignite()`.

Disable it via configuration:

```json
{
  "Ignite": {
    "Azure": {
      "Data": {
        "Tables": {
          "Settings": {
            "HealthChecks": { "Enabled": false }
          }
        }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteAzureTableServiceClient(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark adds the `Azure.Data.Tables.*`
`ActivitySource` to the Ignite OpenTelemetry pipeline, so Table operations appear as spans in your traces.

Disable it via configuration:

```json
{
  "Ignite": {
    "Azure": {
      "Data": {
        "Tables": {
          "Settings": {
            "Tracing": { "Enabled": false }
          }
        }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteAzureTableServiceClient(configureSettings: settings =>
    settings.Tracing.Enabled = false);
```

> [!TIP]
> To ship the spans somewhere, configure an OpenTelemetry exporter through Ignite — for example the
> [Seq exporter Spark](./seq-exporter.md).

### Logging

The `TableServiceClient` is registered through the standard `Microsoft.Extensions.Azure` client factory,
which wires the Azure SDK's `ILogger`-based event source into the app's logging pipeline. Azure Table
client logs therefore flow through the same logging as the rest of your app — including
[Serilog](./serilog.md) when you enable it — with no extra wiring.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Azure Common](./azure-common.md)
- [Azure Blob Storage Spark](./azure-storage-blobs.md)
- [Azure Queue Storage Spark](./azure-storage-queues.md)
- [Azure.Data.Tables documentation](https://learn.microsoft.com/dotnet/api/overview/azure/data.tables-readme)
