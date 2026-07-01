---
title: FluentValidation Spark
description: Wire SharpGrip FluentValidation AutoValidation into an Ignite host so registered validators run automatically for minimal-API endpoints and MVC.
---

## Overview

The FluentValidation Spark turns on [SharpGrip FluentValidation AutoValidation](https://github.com/SharpGrip/FluentValidation.AutoValidation)
inside an Ignite host. Call `builder.IgniteFluentValidation()` and validators you have registered in DI run
automatically against incoming requests — for both minimal-API endpoints and the MVC pipeline — with no
manual `IValidator<T>.Validate(...)` call per action.

Under the hood the Spark:

- Binds a `FluentValidationSparkSettings` from the `Ignite:FluentValidation` configuration section and
  registers it as a singleton.
- When `EndpointsAutoValidationEnabled` is `true`, calls SharpGrip's `AddFluentValidationAutoValidation`
  for endpoints.
- When `MvcAutoValidationEnabled` is `true`, calls SharpGrip's MVC `AddFluentValidationAutoValidation`.

> [!IMPORTANT]
> This Spark enables *AutoValidation*; it does **not** discover or register your validators. You still
> register `IValidator<T>` implementations yourself — typically with
> `services.AddValidatorsFromAssembly(...)` from `FluentValidation.DependencyInjectionExtensions`. To map a
> `ValidationResult` you run manually into the ES.FX `Result`/`Problem` model, see the
> [FluentValidation additions](../../additions/fluentvalidation.md).

> [!NOTE]
> This Spark registers no health check and adds no OpenTelemetry sources — it only wires the AutoValidation
> behavior and its settings.

## Install the client

```bash
dotnet add package ES.FX.Ignite.FluentValidation
```

```xml
<PackageReference Include="ES.FX.Ignite.FluentValidation" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add `Version="…"`.

## Register the client

Call `IgniteFluentValidation` on your host application builder, after `builder.Ignite()`, and register your
validators:

```csharp
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Ignite();

// Register your validators (this is your responsibility, not the Spark's).
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.IgniteFluentValidation();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteFluentValidation(
    this IHostApplicationBuilder builder,
    Action<FluentValidationSparkSettings>? configureSettings = null,
    Action<AutoValidationEndpointsConfiguration>? configureAutoValidationEndpointsConfiguration = null,
    Action<AutoValidationMvcConfiguration>? configureAutoValidationMvcConfiguration = null,
    string configurationSectionPath = FluentValidationSpark.ConfigurationSectionPath);
```

| Parameter | Type | Purpose |
| --- | --- | --- |
| `configureSettings` | `Action<FluentValidationSparkSettings>?` | Customizes the Spark settings after they are read from configuration. |
| `configureAutoValidationEndpointsConfiguration` | `Action<AutoValidationEndpointsConfiguration>?` | Customizes SharpGrip's endpoints AutoValidation configuration. |
| `configureAutoValidationMvcConfiguration` | `Action<AutoValidationMvcConfiguration>?` | Customizes SharpGrip's MVC AutoValidation configuration. |
| `configurationSectionPath` | `string` | Overrides the configuration section. Defaults to `FluentValidationSpark.ConfigurationSectionPath` (`"Ignite:FluentValidation"`). |

`AutoValidationEndpointsConfiguration` and `AutoValidationMvcConfiguration` are SharpGrip types; see the
[SharpGrip AutoValidation documentation](https://github.com/SharpGrip/FluentValidation.AutoValidation) for
their members.

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `FluentValidationSparkSettings` | Singleton | The resolved settings. |
| SharpGrip endpoints AutoValidation | — | Registered when `EndpointsAutoValidationEnabled` is `true`. |
| SharpGrip MVC AutoValidation | — | Registered when `MvcAutoValidationEnabled` is `true`. |

## Configuration

All configuration lives under the `Ignite:FluentValidation` section. This Spark exposes **Settings** only —
there are no separate Options. Per the Ignite settings convention, the settings bind to the
`Ignite:FluentValidation:Settings` sub-node. The `configureSettings` delegate runs **after** configuration
is read, so a delegate overrides the corresponding JSON values.

`FluentValidationSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `EndpointsAutoValidationEnabled` | `bool` | `true` | Enables AutoValidation for minimal-API endpoints. |
| `MvcAutoValidationEnabled` | `bool` | `true` | Enables AutoValidation for the MVC pipeline. |

### Configure via appsettings

```json
{
  "Ignite": {
    "FluentValidation": {
      "Settings": {
        "EndpointsAutoValidationEnabled": true,
        "MvcAutoValidationEnabled": false
      }
    }
  }
}
```

### Configure with delegates

```csharp
builder.IgniteFluentValidation(
    configureSettings: settings =>
    {
        settings.EndpointsAutoValidationEnabled = true;
        settings.MvcAutoValidationEnabled = false;
    },
    configureAutoValidationMvcConfiguration: mvc =>
    {
        // Tune SharpGrip MVC AutoValidation here.
    });
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`FluentValidationSpark.ConfigurationSectionPath` (`"Ignite:FluentValidation"`). Most apps never change it;
supply a custom path only if you want the config to live somewhere other than `Ignite:FluentValidation`.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [FluentValidation additions](../../additions/fluentvalidation.md) — map a `ValidationResult` into the ES.FX `Result`/`Problem` model.
- [SharpGrip AutoValidation documentation](https://github.com/SharpGrip/FluentValidation.AutoValidation) — the upstream AutoValidation library this Spark configures.
- [FluentValidation documentation](https://docs.fluentvalidation.net/) — the upstream validation library.
