---
title: Core extensions
description: BCL-style helpers in the ES.FX core package for arrays, streams, LINQ, reflection, HTTP proxies, and type-kind attributes.
---

The base `ES.FX` package ships a small set of framework-agnostic helpers, organized into folders that
mirror the BCL area they extend: `Collections`, `ComponentModel`, `IO`, `Linq`, `Net`, and `Reflection`.
Each namespace is `ES.FX.{Area}`, so you import exactly the surface you need. This page covers what each
helper does and how to call it.

> [!NOTE]
> These helpers depend on nothing else in the repository — the core `ES.FX` package is the bottom layer.
> You can reference it on its own, without Ignite, Hosting, or any Addition.

## Install

The extensions live in the base `ES.FX` package.

```bash
dotnet add package ES.FX
```

```xml
<PackageReference Include="ES.FX" />
```

> [!NOTE]
> ES.FX uses Central Package Management. When your consuming project also centralizes versions, the
> `<PackageReference>` carries no `Version` attribute. For a standalone consumer, add `Version="…"`.

## Collections

`ES.FX.Collections.ArrayExtensions` adds a null-and-empty guard for arrays.

| Member | Signature | Purpose |
| --- | --- | --- |
| `IsNullOrEmpty` | `bool IsNullOrEmpty(this Array? array)` | `true` when the array is `null` or has zero length. |

```csharp
using ES.FX.Collections;

int[]? values = GetValues();

if (values.IsNullOrEmpty())
{
    // Safe to call on a null reference — the extension handles it.
    return;
}
```

Because it extends `Array`, it works for any array rank or element type.

## IO

`ES.FX.IO.StreamExtensions` reads a stream fully into a `byte[]`, with a fast path for `MemoryStream`.
Both methods read from the stream's **current position** to the end.

| Member | Signature | Purpose |
| --- | --- | --- |
| `ToByteArray` | `byte[] ToByteArray(this Stream stream)` | Reads all bytes from the current position synchronously. |
| `ToByteArrayAsync` | `Task<byte[]> ToByteArrayAsync(this Stream stream, CancellationToken cancellationToken = default)` | Reads all bytes from the current position asynchronously. |

```csharp
using ES.FX.IO;

await using var stream = File.OpenRead("payload.bin");
byte[] bytes = await stream.ToByteArrayAsync(cancellationToken);
```

> [!TIP]
> When the source is a `MemoryStream` positioned at `0`, both methods return its buffer via `ToArray()`
> directly instead of copying through a second stream. A `MemoryStream` at any other position falls
> through to the copy path, so only the remaining bytes are returned.

## Linq

`ES.FX.Linq.EnumerableExtensions` adds a random-pick helper over any `IEnumerable<T>`.

| Member | Signature | Purpose |
| --- | --- | --- |
| `TakeRandomItemOrDefault` | `T? TakeRandomItemOrDefault<T>(this IEnumerable<T?> source)` | Returns a random element, or `default` when the sequence is empty. |

```csharp
using ES.FX.Linq;

string[] regions = ["us-east", "us-west", "eu-central"];
string? region = regions.TakeRandomItemOrDefault();
```

The selection uses `Random.Shared`, so it is thread-safe. The source is enumerated once into a list to
allow indexed access.

## Net

`ES.FX.Net` provides POCO option types plus an extension that turns them into a runtime `IWebProxy`. Use
these to bind HTTP-proxy configuration from `appsettings.json` and hand the result to an `HttpClientHandler`.

| Type / member | Signature | Purpose |
| --- | --- | --- |
| `BasicHttpProxyOptions` | class | Proxy configuration: `Address`, `BypassProxyOnLocal`, `BypassList`, `UseDefaultCredentials`, `Credentials`. |
| `NetworkCredentialOptions` | class | Credential configuration: `UserName`, `Password`, `Domain`. |
| `BuildBasicHttpProxy` | `IWebProxy? BuildBasicHttpProxy(this BasicHttpProxyOptions? options)` | Builds an `IWebProxy` from the options, or `null` when `options` is `null` or `Address` is blank. |

> [!NOTE]
> `BuildBasicHttpProxy` lives in the `ES.FX.Net.Extensions` namespace; the option types live in
> `ES.FX.Net`.

Bind the options from configuration:

```json
{
  "Proxy": {
    "Address": "http://proxy.example.com:8080",
    "BypassProxyOnLocal": true,
    "BypassList": [ "*.internal.example.com" ],
    "UseDefaultCredentials": false,
    "Credentials": {
      "UserName": "svc-proxy",
      "Password": "…",
      "Domain": "CORP"
    }
  }
}
```

Then build the proxy and attach it to an `HttpClient`:

```csharp
using ES.FX.Net;
using ES.FX.Net.Extensions;

var options = configuration.GetSection("Proxy").Get<BasicHttpProxyOptions>();

IWebProxy? proxy = options.BuildBasicHttpProxy();

var handler = new HttpClientHandler
{
    Proxy = proxy,
    UseProxy = proxy is not null
};

using var client = new HttpClient(handler);
```

When `Credentials` is set, the proxy authenticates with a `NetworkCredential` built from
`UserName`/`Password`/`Domain`; otherwise the `UseDefaultCredentials` flag decides — `true` uses the
current default credentials, `false` attaches no credentials.

## Reflection

`ES.FX.Reflection` wraps embedded assembly resources so you can read them without hand-rolling
`GetManifestResourceStream` plumbing.

| Type / member | Signature | Purpose |
| --- | --- | --- |
| `ManifestResource` | `ManifestResource(Assembly assembly, string name)` | Wraps one embedded resource by name. |
| `ManifestResource.Name` | `string Name` | The resource name. |
| `ManifestResource.Info` | `ManifestResourceInfo? Info` | The underlying `ManifestResourceInfo`. |
| `GetStream` | `Stream? GetStream()` | Opens the resource stream, or `null` if not found. |
| `GetStreamReader` | `StreamReader? GetStreamReader()` | A `StreamReader` over the resource, or `null`. |
| `ReadAllBytes` | `byte[]? ReadAllBytes()` | Reads the resource as bytes. |
| `ReadAllBytesAsync` | `Task<byte[]?> ReadAllBytesAsync(CancellationToken cancellation = default)` | Reads the resource as bytes asynchronously. |
| `ReadText` | `string? ReadText()` | Reads the resource as text. |
| `ReadTextAsync` | `Task<string?> ReadTextAsync(CancellationToken cancellationToken = default)` | Reads the resource as text asynchronously. |
| `GetManifestResources` | `ManifestResource[] GetManifestResources(this Assembly assembly)` | Wraps every embedded resource in the assembly. |

Read a single embedded resource by name:

```csharp
using ES.FX.Reflection;

var assembly = typeof(MyType).Assembly;
var resource = new ManifestResource(assembly, "MyProject.Resources.template.json");

string? json = await resource.ReadTextAsync(cancellationToken);
```

Enumerate every embedded resource in an assembly:

```csharp
using ES.FX.Reflection;

foreach (var resource in typeof(MyType).Assembly.GetManifestResources())
{
    Console.WriteLine(resource.Name);
}
```

> [!TIP]
> The `Read*` methods return `null` when the resource name does not resolve, rather than throwing — check
> for `null` before using the result.

## ComponentModel

`ES.FX.ComponentModel.DataAnnotations` provides two type-level attributes that tag a class or interface
with a string "kind". The kind is used for stable lookups during serialization and deserialization
instead of relying on the runtime type name.

| Type / member | Signature | Purpose |
| --- | --- | --- |
| `KindAttribute` | `KindAttribute(string kind)` | Tags a class/interface with a custom type kind. |
| `KindAttribute.Kind` | `string Kind` | The kind value. |
| `KindAttribute.For` | `static string? For(Type type)` / `static string? For<T>()` | Looks up the kind for a type (cached), or `null` if unattributed. |
| `FaultKindAttribute` | `FaultKindAttribute(string kind)` | Tags a class/interface with a fault kind. |
| `FaultKindAttribute.Kind` | `string Kind` | The kind value. |
| `FaultKindAttribute.For` | `static string? For(Type type)` / `static string? For<T>()` | Looks up the fault kind for a type (cached), or `null` if unattributed. |

Both attributes target `AttributeTargets.Class | AttributeTargets.Interface` and cache their lookups per
type, so `For` is cheap to call repeatedly.

```csharp
using ES.FX.ComponentModel.DataAnnotations;

[Kind("order.created.v1")]
public sealed class OrderCreated
{
    public required string OrderId { get; init; }
}

// Resolve the stable kind for serialization/routing:
string? kind = KindAttribute.For<OrderCreated>();   // "order.created.v1"
string? unset = KindAttribute.For(typeof(string));  // null — no attribute
```

`FaultKindAttribute` behaves identically but is intended for fault/error types (for example, mapping an
exception or error contract to a stable kind).

## See also

- [Results and problems](./results-and-problems.md) — the error-handling primitives in the same core package.
- [Primitives](./primitives.md) — `Optional<T>`, `DurationValue`, and `ValueRange`.
- [Conventions and build configuration](./conventions.md) — how the core package is built and packed.
- [Creating a new ES.FX library](./creating-a-library.md) — scaffold a new package that follows these conventions.
