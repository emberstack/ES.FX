---
title: Kubernetes client integration
description: Register a KubernetesClient IKubernetes with Ignite, including a cluster-connectivity health check.
---

## Overview

The Kubernetes client Spark registers an [`IKubernetes`](https://github.com/kubernetes-client/csharp)
client into dependency injection, with a cluster-connectivity health check already wired up. Call
`builder.IgniteKubernetesClient()` once and inject `IKubernetes` anywhere in your app — the client
configuration is built for you (from the ambient kubeconfig or in-cluster service account by default),
and the `/health` probe verifies the cluster is reachable.

Under the hood the Spark:

- Binds a `KubernetesClientSparkOptions` (client tweaks such as `SkipTlsVerify`) and a
  `KubernetesClientSparkSettings` (health-check toggles) from the `Ignite:KubernetesClient`
  configuration section.
- Registers a `KubernetesClientConfiguration` — built via `KubernetesClientConfiguration.BuildDefaultConfig()`
  by default, or from a factory you supply — and an `IKubernetes` client constructed from it.
- Adds a health check that calls the cluster `version` endpoint.

> [!TIP]
> The health check and metadata helpers come from
> [`ES.FX.Additions.KubernetesClient`](../../additions/kubernetesclient.md). This Spark wires that
> `KubernetesHealthCheck` into Ignite for you; the Additions page documents the same helpers for use
> without Ignite.

> [!NOTE]
> This page documents the **client** integration. ES.FX has no AppHost or orchestration layer — the
> Spark connects to whatever cluster your `KubernetesClientConfiguration` resolves (kubeconfig when
> running locally, the in-cluster service account when running inside a pod).

## Install the client

```bash
dotnet add package ES.FX.Ignite.KubernetesClient
```

```xml
<PackageReference Include="ES.FX.Ignite.KubernetesClient" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteKubernetesClient` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteKubernetesClient();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteKubernetesClient(
    this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<KubernetesClientSparkSettings>? configureSettings = null,
    Action<KubernetesClientSparkOptions>? configureOptions = null,
    Func<IServiceProvider, KubernetesClientConfiguration>? kubernetesClientConfigurationFactory = null,
    Action<IServiceProvider, KubernetesClientConfiguration>? configureKubernetesClientConfiguration = null,
    Func<IServiceProvider, DelegatingHandler[]>? kubernetesClientDelegatingHandlers = null,
    ServiceLifetime lifetime = ServiceLifetime.Singleton,
    string configurationSectionPath = KubernetesClientSpark.ConfigurationSectionPath);
```

The extra parameters give you full control over how the client is built:

| Parameter | Purpose |
| --- | --- |
| `kubernetesClientConfigurationFactory` | Supplies the `KubernetesClientConfiguration` instead of the default `KubernetesClientConfiguration.BuildDefaultConfig()`. Use it to point at a specific kubeconfig file, context, or in-cluster config. |
| `configureKubernetesClientConfiguration` | Mutates the resolved `KubernetesClientConfiguration` after it is built (default or factory-produced). Runs after `SkipTlsVerify` is applied (only when a value is set). |
| `kubernetesClientDelegatingHandlers` | A `Func<IServiceProvider, DelegatingHandler[]>` invoked once per `IKubernetes` construction (with the resolving service provider) to build the handlers inserted into the client's HTTP pipeline — for custom auth, logging, or retry handlers. Returning fresh handler instances each time is required for non-`Singleton` lifetimes, since a `DelegatingHandler` cannot be re-parented once it has started a request. |
| `lifetime` | The `ServiceLifetime` for both `IKubernetes` and `KubernetesClientConfiguration`. Defaults to `Singleton`. |

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `IKubernetes` | `lifetime` (default `Singleton`) | The client. Keyed when `serviceKey` is set. |
| `KubernetesClientConfiguration` | `lifetime` (default `Singleton`) | The configuration the client is built from. Keyed when `serviceKey` is set. |
| `KubernetesClientSparkSettings` | Singleton | The resolved health-check settings (keyed by `serviceKey`). |
| Health check `KubernetesClient` | — | Probes the cluster `version` endpoint. See [Health checks](#health-checks). |

### Consume the client

Inject `IKubernetes` and call the typed API groups:

```csharp
public sealed class NamespaceLister(IKubernetes client)
{
    public async Task<IReadOnlyList<string>> ListNamespacesAsync()
    {
        var namespaces = await client.CoreV1.ListNamespaceAsync();
        return namespaces.Items.Select(item => item.Metadata.Name).ToList();
    }
}
```

> [!WARNING]
> Calling `IgniteKubernetesClient` twice with the **same** `serviceKey` throws
> `ReconfigurationNotSupportedException`. Register each client exactly once. To register more than one
> Kubernetes client, give each a distinct `serviceKey` (see below).

### Register keyed clients

To connect to more than one cluster, register each as a **keyed** service with a distinct
`serviceKey`, and pass a matching `name` so each reads its own configuration sub-section:

```csharp
builder.IgniteKubernetesClient(name: "primary", serviceKey: "primary");
builder.IgniteKubernetesClient(name: "dr", serviceKey: "dr");
```

`name` and `serviceKey` do different jobs:

- **`name`** selects the configuration sub-section. `name: "primary"` reads from
  `Ignite:KubernetesClient:primary` instead of `Ignite:KubernetesClient`. It does not affect DI.
- **`serviceKey`** registers `IKubernetes` (and its `KubernetesClientConfiguration`) as a **keyed**
  service. Resolve it with `[FromKeyedServices("…")]`. When `serviceKey` is `null`, the client is the
  default (unkeyed) registration.

The matching configuration:

```json
{
  "Ignite": {
    "KubernetesClient": {
      "primary": {
        "SkipTlsVerify": false
      },
      "dr": {
        "SkipTlsVerify": true
      }
    }
  }
}
```

Resolve the keyed clients by key:

```csharp
public sealed class ClusterProbe(
    [FromKeyedServices("primary")] IKubernetes primary,
    [FromKeyedServices("dr")] IKubernetes dr)
{
    // ...
}
```

> [!IMPORTANT]
> The keyed cluster you point at each `name` is decided by your
> `kubernetesClientConfigurationFactory` (or the ambient default config), not by `appsettings.json` —
> the only client detail bound from configuration is `SkipTlsVerify`, and only when it is set (a `null`
> value leaves the factory/kubeconfig setting untouched). Supply a per-client factory when
> registering distinct clusters.

## Configuration

All configuration lives under the `Ignite:KubernetesClient` section. Both delegates
(`configureSettings`, `configureOptions`) run **after** configuration is read from `appsettings.json`,
so a delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into two types with two separate delegates:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Options** | `KubernetesClientSparkOptions` | The Kubernetes client itself. | `Ignite:KubernetesClient` | `configureOptions` |
| **Settings** | `KubernetesClientSparkSettings` | Ignite observability toggles. | `Ignite:KubernetesClient:Settings` | `configureSettings` |

`KubernetesClientSparkOptions` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `SkipTlsVerify` | `bool?` | `null` | Skips TLS certificate verification on the client connection. Applied to the resolved `KubernetesClientConfiguration` **only when a value is set**; left `null`, the value coming from the resolved configuration (for example a kubeconfig's `insecure-skip-tls-verify` flag or a caller-supplied factory) is preserved. |

> [!WARNING]
> `SkipTlsVerify = true` disables server-certificate validation and exposes the connection to
> man-in-the-middle attacks. Use it only against trusted local clusters, never in production.

`KubernetesClientSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the Kubernetes health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `KubernetesClient` tag. |

> [!NOTE]
> This Spark exposes no `Tracing` or `Metrics` settings — `KubernetesClientSparkSettings` carries only
> the `HealthChecks` toggles. See [Observability](#observability).

### Configure via appsettings

`SkipTlsVerify` sits at the section root; the health-check toggles nest under a `Settings` sub-section:

```json
{
  "Ignite": {
    "KubernetesClient": {
      "SkipTlsVerify": false,
      "Settings": {
        "HealthChecks": {
          "Enabled": true,
          "Timeout": "00:00:05"
        }
      }
    }
  }
}
```

### Configure with delegates

`configureSettings` and `configureOptions` are separate delegates. Both run after `appsettings.json`,
so values set here override the JSON above:

```csharp
builder.IgniteKubernetesClient(
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
    },
    configureOptions: options =>
    {
        options.SkipTlsVerify = false;
    });
```

To build the client from a specific kubeconfig file or context — rather than the ambient default —
supply a `kubernetesClientConfigurationFactory`, and tweak the result with
`configureKubernetesClientConfiguration`:

```csharp
builder.IgniteKubernetesClient(
    kubernetesClientConfigurationFactory: _ =>
        KubernetesClientConfiguration.BuildConfigFromConfigFile(
            kubeconfigPath: "/home/app/.kube/config",
            currentContext: "staging"),
    configureKubernetesClientConfiguration: (_, config) =>
    {
        config.Namespace = "workloads";
    });
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`KubernetesClientSpark.ConfigurationSectionPath` (`"Ignite:KubernetesClient"`). Most apps never change
it; supply a custom path only if you want the Kubernetes config to live somewhere other than
`Ignite:KubernetesClient`.

## Health checks

The Spark registers a health check named **`KubernetesClient`** by default (`HealthChecks.Enabled` is
`true`). For a keyed registration the name carries the key suffix — e.g. `KubernetesClient[primary]`.
The check calls the cluster `version` endpoint (reachable without special RBAC permissions) and reports
`Healthy` when a non-empty `GitVersion` comes back; any failure or empty version reports `Unhealthy`.

The check is tagged `KubernetesClient`, plus any tags you add via `HealthChecks.Tags`. It surfaces at
the health endpoint mapped by `app.Ignite()`.

> [!NOTE]
> This is a **readiness** check. Ignite's liveness probe includes only checks tagged `"live"`; add
> `"live"` via `HealthChecks.Tags` if you want the Kubernetes connectivity check to gate liveness too.

Disable it via configuration:

```json
{
  "Ignite": {
    "KubernetesClient": {
      "Settings": {
        "HealthChecks": { "Enabled": false }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteKubernetesClient(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

This Spark does not register a Kubernetes-specific `ActivitySource`. Requests still flow through the
standard .NET `HttpClient` pipeline, so if you enable HTTP client instrumentation through Ignite's
OpenTelemetry configuration, calls to the Kubernetes API server appear as outgoing HTTP spans. Add a
`DelegatingHandler` through the `kubernetesClientDelegatingHandlers` factory if you need custom span
enrichment.

### Metrics

This Spark emits no Kubernetes-specific metrics and exposes no `Metrics` setting.

### Logging

The Kubernetes client is built with the delegating handlers you pass in and the resolved
`KubernetesClientConfiguration`; the Spark does not attach a dedicated logger to it. To capture request
logging, return a logging `DelegatingHandler` from the `kubernetesClientDelegatingHandlers` factory, which then
flows through your app's configured logging pipeline — including [Serilog](./serilog.md) when you
enable it.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [KubernetesClient additions](../../additions/kubernetesclient.md)
- [Serilog Spark](./serilog.md)
- [KubernetesClient (k8s) documentation](https://github.com/kubernetes-client/csharp)
