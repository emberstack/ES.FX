---
title: API versioning integration
description: Register Asp.Versioning with Ignite, with URL-segment versioning, version reporting, and the API Explorer preconfigured.
---

## Overview

The API Versioning Spark registers the [Asp.Versioning](https://github.com/dotnet/aspnet-api-versioning)
services into dependency injection with sensible defaults, so your API can advertise and route
multiple versions without hand-writing the setup. Call `builder.IgniteApiVersioning()` once and you get:

- **URL-segment versioning** — the version is read from the route (for example `/v1/orders`) via
  `UrlSegmentApiVersionReader`.
- **Version reporting** — responses include `api-supported-versions` / `api-deprecated-versions` headers
  (`ReportApiVersions = true`).
- **API Explorer** — versioned API metadata is exposed for OpenAPI/Swagger tooling, with group names
  formatted as `v1`, `v1.1`, and the version substituted into documented URLs.

Under the hood the Spark calls `services.AddApiVersioning(...).AddApiExplorer(...)`, applies those defaults,
and then invokes your optional delegates so you can override any of them.

> [!TIP]
> Pair this with the [Asp.Versioning additions](../../additions/asp-versioning.md), which adds
> `HasApiVersions(...)` for declaring several supported versions on a minimal-API endpoint in one call.

> [!NOTE]
> This Spark is intentionally minimal. Unlike service Sparks such as
> [Redis](./stackexchange-redis.md), it has no `Ignite:` configuration section, no `SparkSettings` /
> `SparkOptions`, no health check, and no OpenTelemetry sources — API versioning is a routing/metadata
> concern, not a networked dependency. It configures the upstream Asp.Versioning options directly.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Asp.Versioning
```

```xml
<PackageReference Include="ES.FX.Ignite.Asp.Versioning" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteApiVersioning` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteApiVersioning();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteApiVersioning(
    this IHostApplicationBuilder builder,
    Action<ApiVersioningOptions>? configureApiVersioningOptions = null,
    Action<ApiExplorerOptions>? configureApiExplorerOptions = null);
```

Both delegates come from the upstream Asp.Versioning libraries
([`ApiVersioningOptions`](https://github.com/dotnet/aspnet-api-versioning/wiki), `Asp.Versioning`;
[`ApiExplorerOptions`](https://github.com/dotnet/aspnet-api-versioning/wiki), `Asp.Versioning.ApiExplorer`)
and run **after** the Spark applies its defaults, so anything you set in them wins.

### What gets registered

`IgniteApiVersioning` registers the standard Asp.Versioning service graph — the same services
`AddApiVersioning().AddApiExplorer()` produce — with these Spark-applied defaults:

| Setting | Applied value | Effect |
| --- | --- | --- |
| `ApiVersioningOptions.ReportApiVersions` | `true` | Adds `api-supported-versions` / `api-deprecated-versions` response headers. |
| `ApiVersioningOptions.ApiVersionReader` | `new UrlSegmentApiVersionReader()` | Reads the requested version from the URL segment (e.g. `/v1/…`). |
| `ApiExplorerOptions.GroupNameFormat` | `"'v'VVV"` | Formats API Explorer group names as `v1`, `v1.1`, `v2`, etc. |
| `ApiExplorerOptions.SubstituteApiVersionInUrl` | `true` | Replaces the version route token with the concrete version in documented URLs. |

Key DI services this makes available (from Asp.Versioning):

| Service | Purpose |
| --- | --- |
| `IApiVersionDescriptionProvider` | Enumerates the versions your API exposes — the entry point for building one OpenAPI document per version. |
| `IApiVersionDescriptionProviderFactory` | Factory used to construct the provider. |

> [!IMPORTANT]
> The Spark registers the versioning **services**. To version actual endpoints you still declare an API
> version set and apply versions to your routes — either with the upstream `HasApiVersion(...)` /
> `[ApiVersion]`, or with the [`HasApiVersions(...)`](../../additions/asp-versioning.md) helper from the
> Asp.Versioning additions. There is no post-build `app.IgniteApiVersioning()` step.

### Version your endpoints

Resolve `IApiVersionDescriptionProvider` (or the injected version set) and apply versions to your routes.
A minimal example:

```csharp
using Asp.Versioning;
using ES.FX.Additions.Asp.Versioning.Builder;

var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1.0))
    .HasApiVersion(new ApiVersion(2.0))
    .ReportApiVersions()
    .Build();

app.MapGet("/v{version:apiVersion}/weather", () => "forecast")
    .WithApiVersionSet(versionSet)
    .HasApiVersions([new ApiVersion(1.0), new ApiVersion(2.0)]);
```

> [!NOTE]
> `HasApiVersions(...)` targets `IEndpointConventionBuilder` (routes and route groups). The version-set
> builder returned by `NewApiVersionSet()` is not an `IEndpointConventionBuilder`, so configure it with the
> upstream `HasApiVersion(ApiVersion)` as shown above.

To build one OpenAPI/Swagger document per discovered version, inject `IApiVersionDescriptionProvider` and
iterate its `ApiVersionDescriptions`:

```csharp
public sealed class VersionedDocuments(IApiVersionDescriptionProvider provider)
{
    public IEnumerable<string> GroupNames =>
        provider.ApiVersionDescriptions.Select(description => description.GroupName);
}
```

## Configuration

This Spark reads **no** configuration from the `Ignite:` section — it has no `SparkOptions` or
`SparkSettings`. All customization flows through the two upstream-options delegates, which run after the
Spark applies its defaults.

### Configure with delegates

Override any default by mutating the options in `configureApiVersioningOptions` or
`configureApiExplorerOptions`. For example, set a default version so unversioned requests resolve to it, and
assume the default version when a client omits one:

```csharp
builder.IgniteApiVersioning(
    configureApiVersioningOptions: options =>
    {
        options.DefaultApiVersion = new ApiVersion(1.0);
        options.AssumeDefaultVersionWhenUnspecified = true;
    },
    configureApiExplorerOptions: options =>
    {
        // Keep the Spark's group-name format but adjust other API Explorer behavior.
        options.AddApiVersionParametersWhenVersionNeutral = true;
    });
```

You can also swap the version reader if URL-segment versioning is not what you want — for example, read the
version from a header or query string instead:

```csharp
builder.IgniteApiVersioning(configureApiVersioningOptions: options =>
{
    options.ApiVersionReader = ApiVersionReader.Combine(
        new HeaderApiVersionReader("api-version"),
        new QueryStringApiVersionReader("api-version"));
});
```

> [!NOTE]
> Because your delegate runs after the Spark's defaults, setting `ApiVersionReader`,
> `ReportApiVersions`, `GroupNameFormat`, or `SubstituteApiVersionInUrl` here replaces the Spark's value.

## Health checks

This Spark registers **no** health check. API versioning is metadata and routing configuration with no
external dependency to probe, so there is nothing to add to the `app.Ignite()` health endpoints.

## Observability

This Spark adds **no** OpenTelemetry `ActivitySource` or meter of its own, and registers no dedicated
logging category. Versioned requests are traced and logged through ASP.NET Core's normal request pipeline —
the version segment appears in the route like any other path — so they flow through whatever exporters you
configure via Ignite.

## See also

- [Asp.Versioning additions](../../additions/asp-versioning.md) — the `HasApiVersions(...)` helper for
  applying multiple versions to an endpoint.
- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Swashbuckle Spark](./swashbuckle.md) and [NSwag Spark](./nswag.md) — pair versioning with OpenAPI docs.
- [Asp.Versioning documentation](https://github.com/dotnet/aspnet-api-versioning) — the upstream libraries
  this Spark configures.
