---
title: KubernetesClient additions
description: Health check, NamespacedName primitive, and metadata helpers that augment the KubernetesClient (k8s) library.
---

## Overview

`ES.FX.Additions.KubernetesClient` adds a small set of helpers on top of the official
[KubernetesClient](https://github.com/kubernetes-client/csharp) (`k8s`) package: a ready-made
`IHealthCheck` that verifies cluster connectivity, a `NamespacedName` value type for the
`namespace/name` resource identity, and extension methods over `V1ObjectMeta` /
`V1ObjectReference` that fill common gaps when you work with resource metadata.

It does **not** create or configure an `IKubernetes` client — you bring your own (or let the
Ignite Spark register one). This package only augments what the `k8s` library already gives you.

> [!TIP]
> Using Ignite? The [Kubernetes client integration](../ignite/sparks/kubernetesclient.md) registers
> an `IKubernetes` client with the health check below and OpenTelemetry already wired up.

## Install

```bash
dotnet add package ES.FX.Additions.KubernetesClient
```

```xml
<PackageReference Include="ES.FX.Additions.KubernetesClient" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so an in-repo `<PackageReference>` carries no `Version`
> attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

## What it adds

| Member | Signature | Purpose |
| --- | --- | --- |
| `KubernetesHealthCheck` | `KubernetesHealthCheck(IKubernetes client) : IHealthCheck` | Health check that calls the cluster `version` endpoint and reports `Healthy` when a `GitVersion` comes back. |
| `NamespacedName` | `sealed record NamespacedName` | Value type representing a resource identity in `namespace/name` form. |
| `V1ObjectMetaExtensions.NamespacedName` | `NamespacedName NamespacedName(this IKubernetesObject<V1ObjectMeta>? obj)` | Builds a `NamespacedName` from any Kubernetes object. |
| `V1ObjectMetaExtensions.TryGetAnnotationValue<T>` | `bool TryGetAnnotationValue<T>(this V1ObjectMeta metadata, string key, out T? value)` | Reads an annotation and converts it to `T`; returns `false` if missing or unconvertible. |
| `V1ObjectReferenceExtensions.ObjectReference` | `V1ObjectReference ObjectReference(this IKubernetesObject<V1ObjectMeta> obj)` | Builds a fully populated `V1ObjectReference` from an object. |
| `V1ObjectReferenceExtensions.ObjectReference` | `V1ObjectReference ObjectReference(this V1ObjectMeta metadata)` | Builds a partial `V1ObjectReference` from metadata alone. |
| `V1ObjectReferenceExtensions.NamespacedName` | `NamespacedName NamespacedName(this V1ObjectReference? reference)` | Builds a `NamespacedName` from an object reference. |

### `NamespacedName`

`NamespacedName` is an immutable `record` with two properties, `Namespace` and `Name` (both trimmed;
never `null`). It offers several ways to construct and parse an identity:

| Member | Signature | Purpose |
| --- | --- | --- |
| Constructor | `NamespacedName(string? ns, string? name)` | From explicit namespace and name. |
| Constructor | `NamespacedName(string? value)` | Parses `namespace/name` (or bare `name`); throws `ArgumentException` if invalid. |
| Constructor | `NamespacedName(V1ObjectMeta metadata)` | From resource metadata. |
| Constructor | `NamespacedName(IKubernetesObject<V1ObjectMeta> obj)` | From any Kubernetes object. |
| `NamespacedName.Empty` | `static readonly NamespacedName` | Shared empty value for unresolvable references. |
| `TryParse` | `static bool TryParse(string? value, out NamespacedName nsName)` | Non-throwing parse; `nsName` is `Empty` on failure. |
| `ToString` | `override string ToString()` | Renders `namespace/name`, or just `name` when the namespace is empty. |

## Usage

### Register the health check

`KubernetesHealthCheck` takes an `IKubernetes` from DI and calls the cluster version endpoint, which
is generally reachable without special RBAC permissions. Register it against your existing client:

```csharp
builder.Services.AddSingleton<IKubernetes>(_ =>
    new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig()));

builder.Services
    .AddHealthChecks()
    .AddCheck<KubernetesHealthCheck>("kubernetes");
```

The check reports `Healthy` when the cluster returns a non-empty `GitVersion`. It reports the
registration's configured `FailureStatus` (`Unhealthy` by default) when the version call throws (the
exception is attached to the result) and also when the call succeeds but returns no `GitVersion`.
If the caller's `CancellationToken` is cancelled, the resulting `OperationCanceledException`
propagates instead of being converted into a failed result.

### Work with resource identity

```csharp
var pod = await client.CoreV1.ReadNamespacedPodAsync("api", "production");

// From any IKubernetesObject<V1ObjectMeta>
NamespacedName id = pod.NamespacedName();   // production/api
string key = id.ToString();

// Parse from configuration or a label
if (NamespacedName.TryParse("production/api", out var parsed))
{
    // parsed.Namespace == "production", parsed.Name == "api"
}
```

### Read typed annotations

`TryGetAnnotationValue<T>` reads an annotation and converts the raw string to `T` (including
nullable value types), returning `false` when the key is absent or the value cannot be converted:

```csharp
if (pod.Metadata.TryGetAnnotationValue<int>("example.com/replicas", out var replicas))
{
    // replicas holds the converted value
}
```

### Build an object reference

```csharp
// Fully populated (ApiVersion, Kind, Name, Namespace, ResourceVersion, Uid)
V1ObjectReference reference = pod.ObjectReference();

// From metadata only — Name, Namespace, Uid, ResourceVersion
V1ObjectReference partial = pod.Metadata.ObjectReference();
```

> [!NOTE]
> The metadata-only overload cannot populate `ApiVersion` or `Kind` — those live on the object, not
> on `V1ObjectMeta`. Prefer the `IKubernetesObject<V1ObjectMeta>` overload when you have the object.

## Notes and limitations

- This package augments **only** the `k8s` (`KubernetesClient`) library — it does not construct,
  configure, or register an `IKubernetes` client. Provide your own, or use the Ignite Spark.
- `KubernetesHealthCheck` intentionally probes the low-privilege `version` endpoint. It confirms
  connectivity, not that your service account can read the resources you care about.
- `TryGetAnnotationValue<T>` converts using the invariant culture and swallows conversion failures:
  they return `false` rather than surfacing the underlying exception. A `null` `metadata` or `key`
  argument, however, throws `ArgumentNullException`.
- `NamespacedName.TryParse` accepts a bare `name` (no slash) and treats the namespace as empty; the
  string-argument constructor throws `ArgumentException` on anything it cannot parse.

## See also

- [KubernetesClient (k8s) documentation](https://github.com/kubernetes-client/csharp) — the upstream client this package augments.
- [Kubernetes client integration](../ignite/sparks/kubernetesclient.md) — the Ignite Spark that registers `IKubernetes` with this health check wired in.
- [Additions](./index.md) — the full Additions catalog.
- [Health checks additions](./healthchecks.md) — general health-check helpers.
