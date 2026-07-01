---
title: Asp.Versioning additions
description: A helper that applies multiple API versions to a minimal-API endpoint in a single call.
---

## Overview

`ES.FX.Additions.Asp.Versioning` augments the [Asp.Versioning](https://github.com/dotnet/aspnet-api-versioning)
libraries (`Asp.Versioning.Http`, `Asp.Versioning.Mvc`, `Asp.Versioning.Mvc.ApiExplorer`) with a single
convenience: declaring several supported API versions on one endpoint in a single call. Asp.Versioning's
own `HasApiVersion(ApiVersion)` sets one version at a time; this package adds `HasApiVersions(...)` so you
can pass a collection instead of chaining one call per version.

It adds nothing else — all version resolution, reporting, and API Explorer behavior come from the upstream
libraries and their `AddApiVersioning(...)` configuration.

> [!TIP]
> Using Ignite? The [API Versioning Spark](../ignite/sparks/asp-versioning.md) wires Asp.Versioning into
> Ignite so version reporting and the API Explorer are configured for you.

## Install

```bash
dotnet add package ES.FX.Additions.Asp.Versioning
```

```xml
<PackageReference Include="ES.FX.Additions.Asp.Versioning" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so the in-repo `<PackageReference>` carries no `Version`
> attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

## What it adds

| Member | Signature | Purpose |
| --- | --- | --- |
| `EndpointConventionBuilderExtensions.HasApiVersions` | `TBuilder HasApiVersions<TBuilder>(this TBuilder builder, IEnumerable<ApiVersion> apiVersions) where TBuilder : IEndpointConventionBuilder` | Marks the endpoint as supporting every `ApiVersion` in the collection, by calling the upstream `HasApiVersion` for each. Returns the same builder for chaining. |

The extension lives in the `ES.FX.Additions.Asp.Versioning.Builder` namespace and applies to any
`IEndpointConventionBuilder` — a single route, a route group, or anything else Asp.Versioning's conventions
attach to.

## Usage

Declare several supported versions on one endpoint. This is equivalent to calling `HasApiVersion` once per
version, but reads as a single statement:

```csharp
using Asp.Versioning;
using ES.FX.Additions.Asp.Versioning.Builder;

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1.0))
    .HasApiVersion(new ApiVersion(2.0))
    .ReportApiVersions()
    .Build();

app.MapGet("/weather", () => "forecast")
    .WithApiVersionSet(versionSet)
    .HasApiVersions([new ApiVersion(1.0), new ApiVersion(2.0)]);
```

> [!NOTE]
> `HasApiVersions` targets `IEndpointConventionBuilder` (routes and route groups). The version-set
> builder returned by `NewApiVersionSet()` is not an `IEndpointConventionBuilder`, so configure it with
> the upstream `HasApiVersion(ApiVersion)` as shown above.

Because it returns the original builder, it composes with the rest of the Asp.Versioning and minimal-API
convention chain:

```csharp
using ES.FX.Additions.Asp.Versioning.Builder;

var supportedVersions = new[] { new ApiVersion(1.0), new ApiVersion(1.1), new ApiVersion(2.0) };

app.MapGet("/orders", () => Results.Ok())
    .WithApiVersionSet(versionSet)
    .HasApiVersions(supportedVersions)
    .WithName("GetOrders");
```

## Notes and limitations

- This package only adds `HasApiVersions`. Everything else — registering versioning
  (`services.AddApiVersioning(...)`), the API Explorer, version readers, deprecation, and reporting — is
  standard Asp.Versioning. See the [upstream documentation](https://github.com/dotnet/aspnet-api-versioning/wiki)
  for those APIs.
- It is a thin loop over the upstream `HasApiVersion(ApiVersion)`; there is no behavioral difference beyond
  applying the versions in enumeration order. A `null` `apiVersions` collection throws
  `ArgumentNullException`.
- The helper targets `IEndpointConventionBuilder`, so it works with minimal-API endpoints and route groups.
  It does not add controller/MVC-specific helpers.

## See also

- [API Versioning Spark](../ignite/sparks/asp-versioning.md) — the Ignite integration that configures
  Asp.Versioning.
- [Additions](./index.md) — the full catalog of Addition packages.
- [Asp.Versioning documentation](https://github.com/dotnet/aspnet-api-versioning) — the upstream libraries
  this package augments.
