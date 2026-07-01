---
title: NSwag OpenAPI integration
description: Serve an OpenAPI document and a themed Swagger UI in an Ignite web app with a single post-build app.IgniteNSwag() call.
---

## Overview

The NSwag Spark wires [NSwag](https://github.com/RicoSuter/NSwag) into an Ignite web application: it serves
the generated OpenAPI/Swagger document and mounts the Swagger UI. Call `app.IgniteNSwag()` once after
`app.Ignite()` and you get the OpenAPI middleware plus a dark-themed Swagger UI, with the page title and a
few sensible UI defaults filled in from your host environment.

Unlike most Sparks, NSwag is a **web-only, post-build** integration with no builder-phase registration:

- There is **no** `builder.IgniteNSwag()` and **no** `NSwagSpark` config binding — the Spark contributes a
  single middleware-wiring extension method, `app.IgniteNSwag()`, on `WebApplication`.
- It does **not** register the OpenAPI document itself. You register documents the normal NSwag way with
  `builder.Services.AddOpenApiDocument(...)`; the Spark only serves them and hosts the UI.
- It exposes no `Settings`/`Options` types, no health check, and no OpenTelemetry sources. Customization is
  done through the method's parameters and NSwag's own settings delegates.

> [!TIP]
> Pair this Spark with the [NSwag.AspNetCore Addition](../../additions/nswag-aspnetcore.md), whose
> `TypeToStringSchemaNameGenerator` you plug into `AddOpenApiDocument` for stable, namespace-qualified
> schema names. The Addition shapes the document; this Spark serves it and hosts the UI.

## Install the client

```bash
dotnet add package ES.FX.Ignite.NSwag
```

```xml
<PackageReference Include="ES.FX.Ignite.NSwag" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add `Version="…"`.

## Register the client

The NSwag Spark has a single entry point, and it runs in Ignite's **post-build** phase on `WebApplication`.
There is no builder-phase call to add — you register your OpenAPI document(s) with NSwag as usual, then call
`app.IgniteNSwag()` after `app.Ignite()`:

```csharp
using ES.FX.Ignite.NSwag.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Ignite();

// Register the OpenAPI document(s) with NSwag (not the Spark).
builder.Services.AddOpenApiDocument(settings =>
{
    settings.DocumentName = "latest";
});

var app = builder.Build();
app.Ignite();

// Post-build, web-only: serve the OpenAPI document and mount Swagger UI.
app.IgniteNSwag();

await app.RunAsync();
```

> [!IMPORTANT]
> `IgniteNSwag()` is an extension on `WebApplication` and must be called **after** `app.Ignite()`, in the
> post-build phase. It only applies to `WebApplication` hosts — there is nothing to call for non-web hosts.

The full signature is:

```csharp
public static void IgniteNSwag(
    this WebApplication app,
    bool useSwaggerUi = true,
    bool useSwaggerUiDarkMode = true,
    Action<OpenApiDocumentMiddlewareSettings>? configureOpenApiDocumentMiddlewareSettings = null,
    Action<SwaggerUiSettings>? configureSwaggerUiSettings = null);
```

### What gets wired

| Middleware | Added by | Notes |
| --- | --- | --- |
| OpenAPI document endpoint | `app.UseOpenApi(...)` | Serves the JSON document(s) registered via `AddOpenApiDocument`. Customize with `configureOpenApiDocumentMiddlewareSettings`. |
| Swagger UI | `app.UseSwaggerUi(...)` | Mounted only when `useSwaggerUi` is `true` (the default). Customize with `configureSwaggerUiSettings`. |

The Spark applies these UI defaults before invoking `configureSwaggerUiSettings`, so your delegate can
override any of them:

| Setting | Default applied by the Spark |
| --- | --- |
| `CustomInlineStyles` | The `UniversalDark` Swagger theme, when `useSwaggerUiDarkMode` is `true`. |
| `DocumentTitle` | `"{ApplicationName} - Swagger UI"`, from the host `IHostEnvironment.ApplicationName`. |
| `DocExpansion` | `"list"`. |
| `AdditionalSettings["displayRequestDuration"]` | `true`. |

## Parameters

Because NSwag has no `Settings`/`Options` binding, all customization is done through the four method
parameters:

| Parameter | Type | Default | Purpose |
| --- | --- | --- | --- |
| `useSwaggerUi` | `bool` | `true` | Mounts the Swagger UI. Set to `false` to serve the OpenAPI document only, with no UI. |
| `useSwaggerUiDarkMode` | `bool` | `true` | Applies the `UniversalDark` Swagger theme to the UI. Ignored when `useSwaggerUi` is `false`. |
| `configureOpenApiDocumentMiddlewareSettings` | `Action<OpenApiDocumentMiddlewareSettings>?` | `null` | Customizes NSwag's OpenAPI document middleware (route templates, post-process, etc.). Passed straight to `UseOpenApi`. |
| `configureSwaggerUiSettings` | `Action<SwaggerUiSettings>?` | `null` | Customizes NSwag's Swagger UI. Runs **after** the Spark's defaults above, so it overrides them. |

### Serve the document without the UI

To expose the OpenAPI JSON but omit the Swagger UI entirely:

```csharp
app.IgniteNSwag(useSwaggerUi: false);
```

### Keep the UI but disable dark mode

```csharp
app.IgniteNSwag(useSwaggerUiDarkMode: false);
```

### Customize the OpenAPI middleware and UI

Both delegates map directly onto NSwag's own settings types. The UI delegate runs after the Spark's
defaults, so anything you set here wins:

```csharp
app.IgniteNSwag(
    configureOpenApiDocumentMiddlewareSettings: openApi =>
    {
        openApi.Path = "/openapi/{documentName}.json";
    },
    configureSwaggerUiSettings: ui =>
    {
        ui.DocExpansion = "none";
        ui.Path = "/swagger";
    });
```

## Register OpenAPI documents

The Spark serves whatever documents you register with NSwag's `AddOpenApiDocument` in the builder phase — it
does not create documents for you. A typical registration, using the paired Addition's schema-name
generator, looks like:

```csharp
using ES.FX.Additions.NSwag.AspNetCore.Generation;

builder.Services.AddOpenApiDocument(settings =>
{
    settings.DocumentName = "latest";
    settings.Title = builder.Environment.ApplicationName;
    settings.SchemaSettings.SchemaNameGenerator = new TypeToStringSchemaNameGenerator();
});
```

Register `AddOpenApiDocument` more than once to serve multiple documents (for example, one per API version);
`app.IgniteNSwag()` then exposes all of them and lists them in the Swagger UI's document dropdown.

> [!NOTE]
> The NSwag Spark exposes no keyed registration, no `name`/`serviceKey`, and no `configurationSectionPath`
> — those concepts apply to service-client Sparks that bind configuration and register DI services. NSwag is
> a middleware-only integration, so it has none of them.

## Configuration

The NSwag Spark reads **no** `Ignite:NSwag` configuration section. It has no `SparkSettings` or
`SparkOptions` types, so there is nothing to configure through `appsettings.json`. All behaviour is driven by
the `IgniteNSwag()` parameters shown above and by the standard NSwag settings you pass through the two
delegates.

Document generation itself — titles, versions, schema settings, security definitions — is configured on the
NSwag side via `AddOpenApiDocument`, exactly as in any NSwag.AspNetCore app. See the
[NSwag documentation](https://github.com/RicoSuter/NSwag/wiki) for those options.

## Health checks

The NSwag Spark registers **no** health check. Serving API documentation is not a runtime dependency, so
there is nothing to probe. The health endpoints mapped by `app.Ignite()` are unaffected by this Spark.

## Observability

The NSwag Spark adds **no** OpenTelemetry sources — it registers no `ActivitySource` and emits no meters.
Requests to the OpenAPI document and Swagger UI endpoints still flow through the standard ASP.NET Core
request pipeline, so they are captured by Ignite's built-in ASP.NET Core instrumentation like any other HTTP
request; the Spark itself contributes no dedicated tracing or metrics source.

## See also

- [NSwag.AspNetCore Addition](../../additions/nswag-aspnetcore.md) — the paired schema-name generator for `AddOpenApiDocument`.
- [Ignite overview](../index.md) — the two-phase `builder.Ignite()` / `app.Ignite()` model.
- [Sparks catalog](./index.md) — all available Sparks.
- [Swashbuckle Spark](./swashbuckle.md) — the alternative Swagger/OpenAPI toolchain.
- [ASP.NET API versioning Spark](./asp-versioning.md) — pairs well for versioned OpenAPI documents.
- [NSwag documentation](https://github.com/RicoSuter/NSwag/wiki) — the upstream library.
