---
title: Azure Key Vault Secrets client integration
description: Register an Azure SecretClient with Ignite, including a health check and OpenTelemetry tracing.
---

## Overview

The Azure Key Vault Secrets Spark registers an [Azure `SecretClient`](https://learn.microsoft.com/dotnet/api/azure.security.keyvault.secrets.secretclient)
into dependency injection, with a health check and OpenTelemetry tracing already wired up. Call
`builder.IgniteAzureKeyVaultSecretClient()` once and inject `SecretClient` anywhere in your app to read,
set, and manage Key Vault secrets — the vault connection, credential, `/health` probe, and distributed
traces come configured for you.

Under the hood the Spark:

- Binds an `AzureKeyVaultSecretsSparkSettings` (observability toggles) from the
  `Ignite:Azure:KeyVault:Secrets` configuration section.
- Registers a `SecretClient` through the Azure SDK's `AddAzureClients` factory, binding the client's
  `SecretClientOptions` (and the vault URI/credential) straight from that same configuration section.
- Adds a health check that probes the vault.
- Adds the Azure Key Vault Secrets OpenTelemetry `ActivitySource` so vault operations appear in your
  traces.

> [!NOTE]
> This page documents the **client** integration. ES.FX has no AppHost or orchestration layer — you point
> the Spark at an Azure Key Vault you provision yourself, via the vault URI and an Azure credential.

> [!IMPORTANT]
> This Spark builds on the shared [Azure Common](./azure-common.md) helpers, which depend on the Azure SDK
> for .NET (`Microsoft.Extensions.Azure`). The client is created by `AddAzureClients`, so vault
> configuration (the `VaultUri` and credential) follows the standard Azure SDK configuration conventions
> rather than a custom `SparkOptions` type.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Azure.Security.KeyVault.Secrets
```

```xml
<PackageReference Include="ES.FX.Ignite.Azure.Security.KeyVault.Secrets" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteAzureKeyVaultSecretClient` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteAzureKeyVaultSecretClient();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteAzureKeyVaultSecretClient(
    this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<AzureKeyVaultSecretsSparkSettings>? configureSettings = null,
    Action<SecretClientOptions>? configureClientOptions = null,
    string configurationSectionPath = AzureKeyVaultSecretsSpark.ConfigurationSectionPath);
```

> [!NOTE]
> Unlike most Sparks, this one has no `{Service}SparkOptions` type. The client's own configuration is a
> native Azure SDK `SecretClientOptions`, customized through the `configureClientOptions` delegate; the
> vault URI and credential are bound from configuration by the Azure client factory (see
> [Configuration](#configuration)).

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `SecretClient` | Singleton | The vault client. Keyed when `serviceKey` is set. |
| `AzureKeyVaultSecretsSparkSettings` | Singleton | The resolved observability settings (keyed by `serviceKey`). |
| Health check `Azure-SecretClient` | — | Probes the vault. See [Health checks](#health-checks). |
| OpenTelemetry `ActivitySource` | — | `Azure.Security.KeyVault.Secrets.*`. See [Tracing](#tracing). |

### Consume the client

Inject `SecretClient` and use it to read and write secrets:

```csharp
public sealed class SecretReader(SecretClient secretClient)
{
    public async Task<string?> GetAsync(string name)
    {
        KeyVaultSecret secret = await secretClient.GetSecretAsync(name);
        return secret.Value;
    }
}
```

> [!WARNING]
> Calling `IgniteAzureKeyVaultSecretClient` twice with the **same** `serviceKey` throws
> `ReconfigurationNotSupportedException`. Register each vault exactly once. To register more than one
> vault, give each a distinct `serviceKey` (see below).

### Register keyed clients

To connect to more than one vault, register each as a **keyed** service with a distinct `serviceKey`, and
pass a matching `name` so each reads its own configuration sub-section:

```csharp
builder.IgniteAzureKeyVaultSecretClient(name: "app", serviceKey: "app");
builder.IgniteAzureKeyVaultSecretClient(name: "shared", serviceKey: "shared");
```

`name` and `serviceKey` do different jobs:

- **`name`** selects the configuration sub-section. `name: "app"` reads from
  `Ignite:Azure:KeyVault:Secrets:app` instead of `Ignite:Azure:KeyVault:Secrets`. It does not affect DI.
- **`serviceKey`** registers `SecretClient` as a **keyed** singleton. Resolve it with
  `[FromKeyedServices("…")]`. When `serviceKey` is `null`, the client is the default (unkeyed)
  registration.

The matching configuration:

```json
{
  "Ignite": {
    "Azure": {
      "KeyVault": {
        "Secrets": {
          "app": {
            "VaultUri": "https://app-vault.vault.azure.net/"
          },
          "shared": {
            "VaultUri": "https://shared-vault.vault.azure.net/"
          }
        }
      }
    }
  }
}
```

Resolve the keyed clients by key:

```csharp
public sealed class SecretsFacade(
    [FromKeyedServices("app")] SecretClient app,
    [FromKeyedServices("shared")] SecretClient shared)
{
    // ...
}
```

## Configuration

All configuration lives under the `Ignite:Azure:KeyVault:Secrets` section. Both delegates
(`configureSettings`, `configureClientOptions`) run **after** configuration is read from
`appsettings.json`, so a delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into two parts with two separate delegates:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Client options** | `SecretClientOptions` (Azure SDK) | The vault connection: `VaultUri`, credential, and the native `SecretClientOptions`. | `Ignite:Azure:KeyVault:Secrets` | `configureClientOptions` |
| **Settings** | `AzureKeyVaultSecretsSparkSettings` | Ignite observability toggles. | `Ignite:Azure:KeyVault:Secrets:Settings` | `configureSettings` |

The client factory binds the vault URI and the Azure SDK `SecretClientOptions` from the section root
(`VaultUri` plus any standard Azure SDK client-options keys). Credentials follow the standard Azure SDK
resolution (`DefaultAzureCredential` and friends) — see the
[Azure SDK configuration guidance](https://learn.microsoft.com/dotnet/azure/sdk/dependency-injection).

`AzureKeyVaultSecretsSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the vault health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `Azure` and `SecretClient` tags. |
| `Tracing.Enabled` | `bool` | `true` | Adds the Azure Key Vault Secrets tracing source. |

> [!NOTE]
> This Spark exposes no `Metrics` setting — Azure Key Vault Secrets instrumentation here contributes
> tracing only.

### Configure via appsettings

The `VaultUri` and Azure SDK client options sit at the section root; the observability toggles nest under a
`Settings` sub-section:

```json
{
  "Ignite": {
    "Azure": {
      "KeyVault": {
        "Secrets": {
          "VaultUri": "https://my-vault.vault.azure.net/",
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

`configureSettings` and `configureClientOptions` are separate delegates. Both run after
`appsettings.json`, so values set here override the JSON above:

```csharp
builder.IgniteAzureKeyVaultSecretClient(
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
        settings.Tracing.Enabled = true;
    },
    configureClientOptions: options =>
    {
        options.Retry.MaxRetries = 3;
        options.DisableChallengeResourceVerification = false;
    });
```

The `configureClientOptions` delegate customizes the native Azure SDK
[`SecretClientOptions`](https://learn.microsoft.com/dotnet/api/azure.security.keyvault.secrets.secretclientoptions)
(retries, diagnostics, service version) — the vault URI and credential come from configuration.

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`AzureKeyVaultSecretsSpark.ConfigurationSectionPath` (`"Ignite:Azure:KeyVault:Secrets"`). Most apps never
change it; supply a custom path only if you want the vault config to live somewhere else.

## Health checks

The Spark registers a health check named **`Azure-SecretClient`** by default (`HealthChecks.Enabled` is
`true`). For a keyed registration the name carries the key suffix — e.g. `Azure-SecretClient-[app]`. The
check probes the vault by requesting a sentinel secret named `AzureKeyVaultSecretsHealthCheck`; a `404`
(secret not found) is treated as **healthy** because the connection and authorization succeeded, so the
sentinel secret does not need to exist. Any other failure reports the configured failure status.

> [!TIP]
> The probe only needs `Get` permission on that one secret name — no `List` permission is required. Grant
> the app's identity `Get` on secrets (or an equivalent role) so the health check can authenticate.

The check is tagged `Azure` and `SecretClient`, plus any tags you add via `HealthChecks.Tags`. It surfaces
at the health endpoint mapped by `app.Ignite()`.

Disable it via configuration:

```json
{
  "Ignite": {
    "Azure": {
      "KeyVault": {
        "Secrets": {
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
builder.IgniteAzureKeyVaultSecretClient(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark adds the
`Azure.Security.KeyVault.Secrets.*` `ActivitySource` to the Ignite OpenTelemetry pipeline, so vault
operations appear as spans in your traces.

Disable it via configuration:

```json
{
  "Ignite": {
    "Azure": {
      "KeyVault": {
        "Secrets": {
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
builder.IgniteAzureKeyVaultSecretClient(configureSettings: settings =>
    settings.Tracing.Enabled = false);
```

> [!TIP]
> To ship the spans somewhere, configure an OpenTelemetry exporter through Ignite — for example the
> [Seq exporter Spark](./seq-exporter.md).

### Logging

The `SecretClient` is created by the Azure SDK client factory, which participates in the app's configured
logging through `Microsoft.Extensions.Azure`. Azure SDK client logs therefore flow through the same
logging pipeline as the rest of your app — including [Serilog](./serilog.md) when you enable it — with no
extra wiring.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Azure Common](./azure-common.md)
- [Serilog Spark](./serilog.md)
- [Azure Key Vault Secrets client library for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/security.keyvault.secrets-readme)
