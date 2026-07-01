---
title: Azure Blob Storage client integration
description: Register an Azure.Storage.Blobs BlobServiceClient with Ignite, including a health check and OpenTelemetry tracing.
---

## Overview

The Azure Blob Storage Spark registers a shared [Azure.Storage.Blobs](https://learn.microsoft.com/dotnet/api/overview/azure/storage.blobs-readme)
`BlobServiceClient` into dependency injection, with a health check and OpenTelemetry tracing already wired
up. Call `builder.IgniteAzureBlobServiceClient()` once and inject `BlobServiceClient` anywhere in your app
— the connection configuration, the `/health` probe, and distributed traces come for free.

Under the hood the Spark:

- Binds an `AzureBlobStorageSparkSettings` (observability toggles) from the `Ignite:Azure:Storage:Blobs:Settings`
  configuration section.
- Registers a singleton `BlobServiceClient` through the shared Azure client factory
  (`Microsoft.Extensions.Azure`), binding the client's connection configuration (`ServiceUri` /
  `ConnectionString`) and `BlobClientOptions` from the `Ignite:Azure:Storage:Blobs` section.
- Adds a health check that lists blob containers to verify connectivity.
- Adds the Azure Storage Blobs OpenTelemetry `ActivitySource` so blob operations appear in your traces.

> [!NOTE]
> This page documents the **client** integration. ES.FX has no AppHost or orchestration layer — you point
> the Spark at a storage account you provision yourself, via a `ServiceUri` (with a credential) or a
> connection string.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Azure.Storage.Blobs
```

```xml
<PackageReference Include="ES.FX.Ignite.Azure.Storage.Blobs" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteAzureBlobServiceClient` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteAzureBlobServiceClient();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteAzureBlobServiceClient(
    this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<AzureBlobStorageSparkSettings>? configureSettings = null,
    Action<BlobClientOptions>? configureClientOptions = null,
    string configurationSectionPath = AzureBlobStorageSpark.ConfigurationSectionPath);
```

> [!NOTE]
> Unlike some Sparks, the Blob Storage Spark has no dedicated `SparkOptions` type. The underlying client is
> configured through the native `BlobClientOptions` (via `configureClientOptions`) and the standard Azure
> client configuration keys (`ServiceUri` / `ConnectionString`) read from the section — see
> [Configuration](#configuration).

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `BlobServiceClient` | Singleton | The shared blob service client. Keyed when `serviceKey` is set. |
| `AzureBlobStorageSparkSettings` | Singleton | The resolved observability settings (keyed by `serviceKey`). |
| Health check `Azure-BlobServiceClient` | — | Lists blob containers. See [Health checks](#health-checks). |
| OpenTelemetry `ActivitySource` | — | `Azure.Storage.Blobs.*`. See [Tracing](#tracing). |

### Consume the client

Inject `BlobServiceClient` and get a container client from it:

```csharp
public sealed class DocumentStore(BlobServiceClient blobServiceClient)
{
    public async Task UploadAsync(string container, string name, Stream content)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync();
        await containerClient.UploadBlobAsync(name, content);
    }
}
```

> [!WARNING]
> Calling `IgniteAzureBlobServiceClient` twice with the **same** `serviceKey` throws
> `ReconfigurationNotSupportedException`. Register each client exactly once. To register more than one
> storage account, give each a distinct `serviceKey` (see below).

### Register keyed clients

To connect to more than one storage account, register each as a **keyed** service with a distinct
`serviceKey`, and pass a matching `name` so each reads its own configuration sub-section:

```csharp
builder.IgniteAzureBlobServiceClient(name: "documents", serviceKey: "documents");
builder.IgniteAzureBlobServiceClient(name: "media", serviceKey: "media");
```

`name` and `serviceKey` do different jobs:

- **`name`** selects the configuration sub-section. `name: "documents"` reads from
  `Ignite:Azure:Storage:Blobs:documents` instead of `Ignite:Azure:Storage:Blobs`. It does not affect DI.
- **`serviceKey`** registers `BlobServiceClient` as a **keyed** singleton. Resolve it with
  `[FromKeyedServices("…")]`. When `serviceKey` is `null`, the client is the default (unkeyed) registration.

The matching configuration:

```json
{
  "Ignite": {
    "Azure": {
      "Storage": {
        "Blobs": {
          "documents": {
            "ServiceUri": "https://documents.blob.core.windows.net"
          },
          "media": {
            "ServiceUri": "https://media.blob.core.windows.net"
          }
        }
      }
    }
  }
}
```

Resolve the keyed clients by key:

```csharp
public sealed class AssetService(
    [FromKeyedServices("documents")] BlobServiceClient documents,
    [FromKeyedServices("media")] BlobServiceClient media)
{
    // ...
}
```

## Configuration

All Blob Storage configuration lives under the `Ignite:Azure:Storage:Blobs` section. The `configureSettings`
and `configureClientOptions` delegates run **after** configuration is read from `appsettings.json`, so a
delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into the observability **Settings** and the underlying client configuration:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Client configuration** | Azure client keys + `BlobClientOptions` | The storage endpoint and client behavior. | `Ignite:Azure:Storage:Blobs` | `configureClientOptions` |
| **Settings** | `AzureBlobStorageSparkSettings` | Ignite observability toggles. | `Ignite:Azure:Storage:Blobs:Settings` | `configureSettings` |

The client's connection is bound by the shared `Microsoft.Extensions.Azure` factory from these keys at the
section root:

| Key | Purpose |
| --- | --- |
| `ServiceUri` | The blob service endpoint, e.g. `https://<account>.blob.core.windows.net`. Uses the ambient Azure credential (e.g. `DefaultAzureCredential`) for auth. |
| `ConnectionString` | A storage account connection string, as an alternative to `ServiceUri`. |

`AzureBlobStorageSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the blob health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `Azure` and `BlobServiceClient` tags. |
| `Tracing.Enabled` | `bool` | `true` | Adds the Azure Storage Blobs tracing source. |

### Configure via appsettings

The client keys sit at the section root; the observability toggles nest under a `Settings` sub-section:

```json
{
  "Ignite": {
    "Azure": {
      "Storage": {
        "Blobs": {
          "ServiceUri": "https://myaccount.blob.core.windows.net",
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

`configureSettings` customizes the observability toggles; `configureClientOptions` customizes the native
`BlobClientOptions`. Both run after `appsettings.json`, so values set here override the JSON above:

```csharp
builder.IgniteAzureBlobServiceClient(
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
        settings.Tracing.Enabled = true;
    },
    configureClientOptions: options =>
    {
        options.Retry.MaxRetries = 5;
    });
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`AzureBlobStorageSpark.ConfigurationSectionPath` (`"Ignite:Azure:Storage:Blobs"`). Most apps never change
it; supply a custom path only if you want the blob config to live somewhere else.

## Health checks

The Spark registers a health check named **`Azure-BlobServiceClient`** by default
(`HealthChecks.Enabled` is `true`). For a keyed registration the name carries the key suffix — e.g.
`Azure-BlobServiceClient-[documents]`. The check lists blob containers (page size 1) to verify the client
can reach the storage account.

> [!NOTE]
> The check deliberately uses a container-list probe rather than `GetPropertiesAsync`, so it succeeds with
> the least-privileged **Storage Blob Data Reader** role at the account level.

The check is tagged `Azure` and `BlobServiceClient`, plus any tags you add via `HealthChecks.Tags`. It
surfaces at the health endpoint mapped by `app.Ignite()`.

Disable it via configuration:

```json
{
  "Ignite": {
    "Azure": {
      "Storage": {
        "Blobs": {
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
builder.IgniteAzureBlobServiceClient(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark adds the `Azure.Storage.Blobs.*`
`ActivitySource` to the Ignite OpenTelemetry pipeline, so blob operations appear as spans in your traces.

Disable it via configuration:

```json
{
  "Ignite": {
    "Azure": {
      "Storage": {
        "Blobs": {
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
builder.IgniteAzureBlobServiceClient(configureSettings: settings =>
    settings.Tracing.Enabled = false);
```

> [!TIP]
> To ship the spans somewhere, configure an OpenTelemetry exporter through Ignite — for example the
> [Seq exporter Spark](./seq-exporter.md).

### Logging

The `BlobServiceClient` is built by the shared `Microsoft.Extensions.Azure` client factory, which wires the
Azure SDK's `EventSource` logging into the app's configured logging pipeline. Blob client logs therefore
flow through the same pipeline as the rest of your app — including [Serilog](./serilog.md) when you enable
it — with no extra wiring.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Azure common Spark](./azure-common.md)
- [Azure Storage Queues Spark](./azure-storage-queues.md)
- [Serilog Spark](./serilog.md)
- [Azure.Storage.Blobs documentation](https://learn.microsoft.com/dotnet/api/overview/azure/storage.blobs-readme)
