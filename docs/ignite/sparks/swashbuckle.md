---
title: Swagger (Swashbuckle) integration
description: Generate an OpenAPI document and serve the Swagger UI in Ignite with Swashbuckle, toggled from configuration.
---

## Overview

The Swashbuckle Spark wires [Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
into Ignite so your API exposes an OpenAPI document and the interactive Swagger UI. Call
`builder.IgniteSwashbuckle()` before the host is built to register the generator, then
`app.IgniteSwashbuckle()` after the host is built to serve the document and UI — the middleware is
toggleable from configuration, so you can leave the calls in place and turn Swagger on or off per
environment.

Under the hood the Spark:

- Binds a `SwashbuckleSparkSettings` (observability/feature toggles) from the `Ignite:Swashbuckle`
  configuration section and registers it as a singleton.
- Registers the ASP.NET Core endpoint explorer (`AddEndpointsApiExplorer`) and the Swagger generator
  (`AddSwaggerGen`), exposing the `SwaggerGenOptions` for customization.
- In the post-build step, conditionally serves the OpenAPI document (`UseSwagger`) and the Swagger UI
  (`UseSwaggerUI`) based on the bound settings.

> [!IMPORTANT]
> This Spark is a **two-call** integration. The builder-phase `builder.IgniteSwashbuckle(...)` registers
> the generator; the post-build `app.IgniteSwashbuckle(...)` adds the middleware. The post-build call is a
> `WebApplication` extension — it only applies to web hosts. Without it, the document and UI are never
> served.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Swashbuckle
```

```xml
<PackageReference Include="ES.FX.Ignite.Swashbuckle" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteSwashbuckle` on your host application builder after `builder.Ignite()`, then call the
`WebApplication` overload after `app.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteSwashbuckle();

var app = builder.Build();
app.Ignite();
app.IgniteSwashbuckle();

await app.RunAsync();
```

> [!IMPORTANT]
> Order matters. `builder.IgniteSwashbuckle()` runs pre-build (Phase A) and `app.IgniteSwashbuckle()` runs
> post-build (Phase B), after `app.Ignite()`. Placing the middleware call after `app.Ignite()` keeps the
> Swagger endpoints behind Ignite's exception handling and status-code pages.

The builder-phase signature is:

```csharp
public static void IgniteSwashbuckle(
    this IHostApplicationBuilder builder,
    Action<SwashbuckleSparkSettings>? configureSettings = null,
    Action<SwaggerGenOptions>? configureSwaggerGenOptions = null,
    string configurationSectionPath = SwashbuckleSpark.ConfigurationSectionPath);
```

The post-build (`WebApplication`) signature is:

```csharp
public static void IgniteSwashbuckle(
    this WebApplication app,
    Action<SwaggerOptions>? configureSwaggerOptions = null,
    Action<SwaggerUIOptions>? configureSwaggerUIOptions = null);
```

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `SwashbuckleSparkSettings` | Singleton | The resolved feature toggles, consumed by the post-build middleware. |
| Endpoints API explorer | — | Registered via `AddEndpointsApiExplorer()` so minimal-API and controller endpoints are discovered. |
| Swagger generator | — | Registered via `AddSwaggerGen(...)`; customize through the `configureSwaggerGenOptions` delegate. |

> [!WARNING]
> Calling `builder.IgniteSwashbuckle()` twice throws `ReconfigurationNotSupportedException` — the Spark
> guards its configuration key (`Swashbuckle`). Register it exactly once. This Spark takes no `serviceKey`
> and registers no keyed services.

### Customize the generated document

Pass `configureSwaggerGenOptions` to shape the OpenAPI document — document metadata, security schemes, XML
comments, and so on:

```csharp
builder.IgniteSwashbuckle(configureSwaggerGenOptions: options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Orders API",
        Version = "v1"
    });
});
```

Pass `configureSwaggerOptions` and `configureSwaggerUIOptions` on the post-build call to customize how the
document and UI are served (route templates, endpoint list, UI options):

```csharp
app.IgniteSwashbuckle(
    configureSwaggerUIOptions: ui =>
    {
        ui.SwaggerEndpoint("/swagger/v1/swagger.json", "Orders API v1");
        ui.RoutePrefix = "docs";
    });
```

## Configuration

All Swashbuckle configuration lives under the `Ignite:Swashbuckle` section. The `configureSettings`
delegate runs **after** configuration is read from `appsettings.json`, so a delegate overrides the
corresponding JSON values.

### Settings vs options

This Spark exposes **Settings** only — there is no `SwashbuckleSparkOptions` type. Swagger generation and
UI behavior are customized through the strongly typed `SwaggerGenOptions` / `SwaggerOptions` /
`SwaggerUIOptions` delegates on the registration calls (shown above), not through a bound options section.

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Settings** | `SwashbuckleSparkSettings` | Toggles for serving the OpenAPI document and the Swagger UI. | `Ignite:Swashbuckle:Settings` | `configureSettings` |

`SwashbuckleSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `SwaggerEnabled` | `bool` | `true` | When `true`, `app.IgniteSwashbuckle()` serves the OpenAPI document via `UseSwagger`. |
| `SwaggerUIEnabled` | `bool` | `true` | When `true`, `app.IgniteSwashbuckle()` serves the interactive Swagger UI via `UseSwaggerUI`. |

> [!NOTE]
> Both toggles are read by the post-build `app.IgniteSwashbuckle()` call. Setting either to `false`
> disables only that middleware; the generator is still registered by the builder-phase call.

> [!WARNING]
> Both `SwaggerEnabled` and `SwaggerUIEnabled` default to `true` **with no environment gating** — the
> OpenAPI document and the interactive Swagger UI are served in **every** environment, including
> **Production**, unless you turn them off. This exposes your full API surface. It is recommended to
> disable them in Production, for example by setting `SwaggerEnabled` and/or `SwaggerUIEnabled` to
> `false` in `appsettings.Production.json` (see the example below).

### Configure via appsettings

The feature toggles nest under a `Settings` sub-section:

```json
{
  "Ignite": {
    "Swashbuckle": {
      "Settings": {
        "SwaggerEnabled": true,
        "SwaggerUIEnabled": true
      }
    }
  }
}
```

For example, to serve the OpenAPI document but hide the UI in production, set `SwaggerUIEnabled` to
`false` in that environment's `appsettings.Production.json`.

### Configure with delegates

`configureSettings` runs after `appsettings.json`, so values set here override the JSON above:

```csharp
builder.IgniteSwashbuckle(configureSettings: settings =>
{
    settings.SwaggerEnabled = true;
    settings.SwaggerUIEnabled = false;
});
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`SwashbuckleSpark.ConfigurationSectionPath` (`"Ignite:Swashbuckle"`). Most apps never change it; supply a
custom path only if you want the Swashbuckle config to live somewhere other than `Ignite:Swashbuckle`.

## Health checks

This Spark registers **no** health check. It contributes API documentation only and does not add a probe
to the readiness or liveness endpoints mapped by `app.Ignite()`.

## Observability

The Swashbuckle Spark adds **no** OpenTelemetry tracing source and emits **no** metrics — it produces API
documentation, not runtime telemetry. Any requests to the Swagger document and UI endpoints are still
captured by Ignite's ASP.NET Core HTTP instrumentation like any other request. There is no
`Tracing`/`Metrics` setting on `SwashbuckleSparkSettings`.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [NSwag Spark](./nswag.md)
- [Asp.Versioning Spark](./asp-versioning.md)
- [Swashbuckle.AspNetCore documentation](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
