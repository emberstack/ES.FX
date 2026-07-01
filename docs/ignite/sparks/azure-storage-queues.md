---
title: Azure Queue Storage client integration
description: Register an Azure.Storage.Queues QueueServiceClient with Ignite, including a health check and OpenTelemetry tracing.
---

## Overview

The Azure Queue Storage Spark registers a shared [Azure.Storage.Queues](https://learn.microsoft.com/dotnet/api/overview/azure/storage.queues-readme)
`QueueServiceClient` into dependency injection, with a health check and OpenTelemetry tracing already wired
up. Call `builder.IgniteAzureQueueServiceClient()` once and inject `QueueServiceClient` (or resolve a
`QueueClient` from it) anywhere in your app — client configuration, the `/health` probe, and distributed
traces come for free.

The Spark:

- Registers a `QueueServiceClient` through the Azure SDK's `AddAzureClients` factory, binding its
  configuration (connection string or service URI, credentials, retry options) from the
  `Ignite:Azure:Storage:Queues` configuration section.
- Binds an `AzureQueueStorageSparkSettings` (observability toggles) from the same section.
- Adds a health check that probes the queue service with a least-privilege list operation.
- Adds the Azure Queue Storage OpenTelemetry `ActivitySource` so queue operations appear in your traces.

> [!NOTE]
> This page documents the **client** integration. ES.FX has no AppHost or orchestration layer — you point
> the Spark at an Azure Storage account you provision yourself (or the local Azurite emulator) via a
> connection string or service URI.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Azure.Storage.Queues
```

```xml
<PackageReference Include="ES.FX.Ignite.Azure.Storage.Queues" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add `Version="…"`.

## Register the client

Call `IgniteAzureQueueServiceClient` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteAzureQueueServiceClient();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteAzureQueueServiceClient(
    this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<AzureQueueStorageSparkSettings>? configureSettings = null,
    Action<QueueClientOptions>? configureClientOptions = null,
    string configurationSectionPath = AzureQueueStorageSpark.ConfigurationSectionPath);
```

> [!NOTE]
> Unlike Sparks that expose a dedicated `{Service}SparkOptions` type, this Spark configures the client
> through the Azure SDK's native [`QueueClientOptions`](https://learn.microsoft.com/dotnet/api/azure.storage.queues.queueclientoptions).
> The `configureClientOptions` delegate customizes those options directly.

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `QueueServiceClient` | Singleton | The shared queue service client. Keyed when `serviceKey` is set. |
| `AzureQueueStorageSparkSettings` | Singleton | The resolved observability settings (keyed by `serviceKey`). |
| Health check `Azure-QueueServiceClient` | — | Probes the queue service. See [Health checks](#health-checks). |
| OpenTelemetry `ActivitySource` | — | `Azure.Storage.Queues.*`. See [Tracing](#tracing). |

### Consume the client

Inject `QueueServiceClient` and get a `QueueClient` from it:

```csharp
public sealed class NotificationQueue(QueueServiceClient serviceClient)
{
    private readonly QueueClient _queue = serviceClient.GetQueueClient("notifications");

    public async Task EnqueueAsync(string message)
    {
        await _queue.CreateIfNotExistsAsync();
        await _queue.SendMessageAsync(message);
    }
}
```

> [!WARNING]
> Calling `IgniteAzureQueueServiceClient` twice with the **same** `serviceKey` throws
> `ReconfigurationNotSupportedException`. Register each client exactly once. To register more than one queue
> service client, give each a distinct `serviceKey` (see below).

### Register keyed clients

To connect to more than one storage account, register each as a **keyed** service with a distinct
`serviceKey`, and pass a matching `name` so each reads its own configuration sub-section:

```csharp
builder.IgniteAzureQueueServiceClient(name: "primary", serviceKey: "primary");
builder.IgniteAzureQueueServiceClient(name: "audit", serviceKey: "audit");
```

`name` and `serviceKey` do different jobs:

- **`name`** selects the configuration sub-section. `name: "primary"` reads from
  `Ignite:Azure:Storage:Queues:primary` instead of `Ignite:Azure:Storage:Queues`. It does not affect DI.
- **`serviceKey`** registers `QueueServiceClient` as a **keyed** singleton. Resolve it with
  `[FromKeyedServices("…")]`. When `serviceKey` is `null`, the client is the default (unkeyed) registration.

The matching configuration:

```json
{
  "Ignite": {
    "Azure": {
      "Storage": {
        "Queues": {
          "primary": {
            "ServiceUri": "https://primaryaccount.queue.core.windows.net/"
          },
          "audit": {
            "ServiceUri": "https://auditaccount.queue.core.windows.net/"
          }
        }
      }
    }
  }
}
```

Resolve the keyed clients by key:

```csharp
public sealed class QueueRouter(
    [FromKeyedServices("primary")] QueueServiceClient primary,
    [FromKeyedServices("audit")] QueueServiceClient audit)
{
    // ...
}
```

## Configuration

All Azure Queue Storage configuration lives under the `Ignite:Azure:Storage:Queues` section. Both delegates
(`configureSettings`, `configureClientOptions`) run **after** configuration is read from `appsettings.json`,
so a delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into two parts, each with its own delegate:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Options** | `QueueClientOptions` + Azure client config | The queue service connection: connection string / service URI, credentials, retry and transport options. | `Ignite:Azure:Storage:Queues` | `configureClientOptions` |
| **Settings** | `AzureQueueStorageSparkSettings` | Ignite observability toggles. | `Ignite:Azure:Storage:Queues:Settings` | `configureSettings` |

The client connection is bound by the Azure SDK's client factory from the section root. Common keys:

| Key | Purpose |
| --- | --- |
| `ConnectionString` | An Azure Storage connection string, e.g. `UseDevelopmentStorage=true;` for the Azurite emulator, or a full account connection string. |
| `ServiceUri` | The queue service endpoint, e.g. `https://myaccount.queue.core.windows.net/`. Used for identity-based auth (managed identity, `DefaultAzureCredential`) when no connection string is supplied. |

`AzureQueueStorageSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the queue service health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `Azure` and `QueueServiceClient` tags. |
| `Tracing.Enabled` | `bool` | `true` | Adds the Azure Queue Storage tracing source. |

> [!NOTE]
> This Spark exposes no `Metrics` setting — Azure Queue Storage instrumentation here contributes tracing
> only.

### Configure via appsettings

The client connection keys sit at the section root; the observability toggles nest under a `Settings`
sub-section:

```json
{
  "Ignite": {
    "Azure": {
      "Storage": {
        "Queues": {
          "ConnectionString": "UseDevelopmentStorage=true;",
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

### Configure with delegates

`configureSettings` and `configureClientOptions` are separate delegates. Both run after `appsettings.json`,
so values set here override the JSON above:

```csharp
builder.IgniteAzureQueueServiceClient(
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
        settings.Tracing.Enabled = true;
    },
    configureClientOptions: options =>
    {
        options.Retry.MaxRetries = 5;
        options.MessageEncoding = QueueMessageEncoding.Base64;
    });
```

> [!TIP]
> For identity-based authentication, supply `ServiceUri` in configuration and let the Azure SDK resolve a
> credential (for example `DefaultAzureCredential`). Use `configureClientOptions` for transport, retry, and
> message-encoding tuning — not for credentials.

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`AzureQueueStorageSpark.ConfigurationSectionPath` (`"Ignite:Azure:Storage:Queues"`). Most apps never change
it; supply a custom path only if you want the queue configuration to live somewhere else.

## Health checks

The Spark registers a health check named **`Azure-QueueServiceClient`** by default (`HealthChecks.Enabled`
is `true`). For a keyed registration the name carries the key suffix — e.g. `Azure-QueueServiceClient-[audit]`.
The check lists queues with a page size of 1, a least-privilege probe that succeeds with the
**Storage Queue Data Reader** role assignment (it deliberately avoids `GetPropertiesAsync`, which requires
elevated permissions).

The check is tagged `Azure` and `QueueServiceClient`, plus any tags you add via `HealthChecks.Tags`. It
surfaces at the health endpoint mapped by `app.Ignite()`.

Disable it via configuration:

```json
{
  "Ignite": {
    "Azure": {
      "Storage": {
        "Queues": {
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
builder.IgniteAzureQueueServiceClient(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark adds the `Azure.Storage.Queues.*`
`ActivitySource` to the Ignite OpenTelemetry pipeline, so queue operations appear as spans in your traces.

Disable it via configuration:

```json
{
  "Ignite": {
    "Azure": {
      "Storage": {
        "Queues": {
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
builder.IgniteAzureQueueServiceClient(configureSettings: settings =>
    settings.Tracing.Enabled = false);
```

> [!TIP]
> To ship the spans somewhere, configure an OpenTelemetry exporter through Ignite — for example the
> [Seq exporter Spark](./seq-exporter.md).

### Logging

The Azure SDK client is registered through `AddAzureClients`, so `QueueServiceClient` participates in the
app's configured logging pipeline — including [Serilog](./serilog.md) when you enable it — with no extra
wiring.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Azure Blob Storage Spark](./azure-storage-blobs.md)
- [Serilog Spark](./serilog.md)
- [Azure.Storage.Queues documentation](https://learn.microsoft.com/dotnet/api/overview/azure/storage.queues-readme)
