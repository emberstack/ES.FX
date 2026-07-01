---
title: Seq OpenTelemetry exporter integration
description: Export Ignite logs and traces to Seq over OTLP, with an opt-in switch and a Seq health check.
---

## Overview

The Seq OpenTelemetry exporter Spark ships your Ignite [logs](https://docs.datalust.co/docs/an-overview-of-seq)
and traces to a [Seq](https://datalust.co/seq) server over OTLP. Call
`builder.IgniteSeqOpenTelemetryExporter()`, point it at your Seq ingestion endpoint, and the log records and
activity spans that Ignite already collects start flowing to Seq — no manual OpenTelemetry processor wiring.

Under the hood the Spark:

- Binds a `SeqOpenTelemetryExporterSparkOptions` (Seq endpoint, protocol, API key, OTLP exporter tuning) and
  a `SeqOpenTelemetryExporterSparkSettings` (enable switch and observability toggles) from the
  `Ignite:OpenTelemetry:Exporter:Seq` configuration section.
- Adds an `OtlpLogExporter` processor to the Ignite logging pipeline (when log export is enabled).
- Adds an `OtlpTraceExporter` processor to the Ignite tracing pipeline (when trace export is enabled).
- Adds an HTTP health check that probes the Seq server's health URL.

Unlike a client Spark, this Spark registers **no DI service** you resolve — it extends the OpenTelemetry
pipeline that `builder.Ignite()` sets up. It also does not take a `serviceKey`, because there is no keyed
client to register.

> [!IMPORTANT]
> The Spark is **opt-in**: `Settings.Enabled` defaults to `false`. Until you set it to `true` (via
> `appsettings.json` or `configureSettings`), the Spark only binds configuration (the keyed settings
> singleton and the options binding) — it adds **no exporters and no health check**. See
> [Enable the exporter](#enable-the-exporter).

## Install the client

```bash
dotnet add package ES.FX.Ignite.OpenTelemetry.Exporter.Seq
```

```xml
<PackageReference Include="ES.FX.Ignite.OpenTelemetry.Exporter.Seq" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteSeqOpenTelemetryExporter` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteSeqOpenTelemetryExporter();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteSeqOpenTelemetryExporter(
    this IHostApplicationBuilder builder,
    string? name = null,
    Action<SeqOpenTelemetryExporterSparkSettings>? configureSettings = null,
    Action<SeqOpenTelemetryExporterSparkOptions>? configureOptions = null,
    string configurationSectionPath = SeqOpenTelemetryExporterSpark.ConfigurationSectionPath);
```

> [!IMPORTANT]
> Register this Spark **after** `builder.Ignite()`. Ignite establishes the OpenTelemetry logging and tracing
> providers in phase A; this Spark attaches its exporter processors to them, so Ignite must run first.

### What gets registered

| Registration | Kind | Notes |
| --- | --- | --- |
| `SeqOpenTelemetryExporterSparkSettings` | Keyed singleton | The resolved settings, keyed by `name`. |
| `SeqOpenTelemetryExporterSparkOptions` | Named options | Bound via `IOptionsMonitor<T>`, resolved by `name`. |
| OTLP log exporter processor | OpenTelemetry logging processor | Added when `Enabled` and `LogExporterEnabled` are `true`. |
| OTLP trace exporter processor | OpenTelemetry tracing processor | Added when `Enabled` and `TracesExporterEnabled` are `true`. |
| Health check `SeqOpenTelemetryExporter` | — | HTTP probe of the Seq health URL. Added only when `Enabled` **and** `HealthChecks.Enabled` are `true` and a `HealthUrl` is configured. See [Health checks](#health-checks). |

> [!NOTE]
> There is no client type to inject. Configure the Spark, and your existing logging and tracing flow to Seq.
> To read the resolved settings you can inject
> `[FromKeyedServices(null)] SeqOpenTelemetryExporterSparkSettings`, but most apps never need to.

> [!WARNING]
> Calling `IgniteSeqOpenTelemetryExporter` twice with the **same** `name` throws
> `ReconfigurationNotSupportedException`. Register each exporter exactly once per name.

### Register named exporters

This Spark takes a `name` but **no** `serviceKey` (there is no keyed client). Use `name` to bind a distinct
configuration sub-section — useful when you export to more than one Seq instance:

```csharp
builder.IgniteSeqOpenTelemetryExporter(name: "primary");
builder.IgniteSeqOpenTelemetryExporter(name: "audit");
```

`name` selects the configuration sub-section: `name: "primary"` reads from
`Ignite:OpenTelemetry:Exporter:Seq:primary` instead of `Ignite:OpenTelemetry:Exporter:Seq`. It also keys the
settings registration and adds a `[name]` suffix to the health-check name (e.g.
`SeqOpenTelemetryExporter[primary]`).

The matching configuration:

```json
{
  "Ignite": {
    "OpenTelemetry": {
      "Exporter": {
        "Seq": {
          "primary": {
            "IngestionEndpoint": "http://localhost:5341",
            "Settings": { "Enabled": true }
          },
          "audit": {
            "IngestionEndpoint": "http://audit-seq:5341",
            "Settings": { "Enabled": true }
          }
        }
      }
    }
  }
}
```

## Configuration

All Seq exporter configuration lives under the `Ignite:OpenTelemetry:Exporter:Seq` section. Both delegates
(`configureSettings`, `configureOptions`) run **after** configuration is read from `appsettings.json`, so a
delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into two types with two separate delegates:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Options** | `SeqOpenTelemetryExporterSparkOptions` | The Seq connection and OTLP exporter tuning. | `Ignite:OpenTelemetry:Exporter:Seq` | `configureOptions` |
| **Settings** | `SeqOpenTelemetryExporterSparkSettings` | Enable switch and observability toggles. | `Ignite:OpenTelemetry:Exporter:Seq:Settings` | `configureSettings` |

`SeqOpenTelemetryExporterSparkOptions` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `IngestionEndpoint` | `string?` | none | The Seq OTLP ingestion endpoint, e.g. `http://localhost:5341`. For `HttpProtobuf`, the Spark appends `/ingest/otlp/v1/logs` and `/ingest/otlp/v1/traces` automatically. |
| `OtlpProtocol` | `OtlpExportProtocol` | `HttpProtobuf` | The OTLP protocol. Overrides the protocol on both OTLP exporter option sets below. |
| `HealthUrl` | `string?` | none | The Seq health URL probed by the health check. When not set, the health check is not registered. |
| `ApiKey` | `string?` | none | The Seq API key, appended as the `X-Seq-ApiKey` header on both the log and trace exporters (`OtlpLogExporter.Headers` and `OtlpTraceExporter.Headers`). |
| `OtlpLogExporter` | `OtlpExporterOptions` | new instance | Native OTLP exporter options for logs (endpoint, headers, `ExportProcessorType`, timeouts). |
| `OtlpTraceExporter` | `OtlpExporterOptions` | new instance | Native OTLP exporter options for traces. |

`SeqOpenTelemetryExporterSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `false` | Master switch. When `false`, no exporters are added. Must be `true` for anything to export. |
| `LogExporterEnabled` | `bool` | `true` | Adds the OTLP log exporter (only when `Enabled` is `true`). |
| `TracesExporterEnabled` | `bool` | `true` | Adds the OTLP trace exporter (only when `Enabled` is `true`). |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the Seq health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `SeqOpenTelemetryExporter` tag. |

> [!NOTE]
> This Spark has no `Metrics` setting and adds no metrics exporter — it exports logs and traces only.

### Enable the exporter

Because `Enabled` defaults to `false`, the exporter does nothing until you turn it on. Set it via
`appsettings.json`:

```json
{
  "Ignite": {
    "OpenTelemetry": {
      "Exporter": {
        "Seq": {
          "Settings": { "Enabled": true }
        }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteSeqOpenTelemetryExporter(configureSettings: settings =>
    settings.Enabled = true);
```

### Configure via appsettings

Options sit at the section root; the enable switch and observability toggles nest under a `Settings`
sub-section:

```json
{
  "Ignite": {
    "OpenTelemetry": {
      "Exporter": {
        "Seq": {
          "IngestionEndpoint": "http://localhost:5341",
          "HealthUrl": "http://localhost:5341/health",
          "ApiKey": "your-seq-api-key",
          "Settings": {
            "Enabled": true,
            "LogExporterEnabled": true,
            "TracesExporterEnabled": true,
            "HealthChecks": {
              "Enabled": true,
              "Timeout": "00:00:05"
            }
          }
        }
      }
    }
  }
}
```

### Configure with delegates

`configureSettings` and `configureOptions` are separate delegates. Both run after `appsettings.json`, so
values set here override the JSON above:

```csharp
builder.IgniteSeqOpenTelemetryExporter(
    configureSettings: settings =>
    {
        settings.Enabled = true;
        settings.TracesExporterEnabled = false; // logs only
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
    },
    configureOptions: options =>
    {
        options.IngestionEndpoint = "http://localhost:5341";
        options.HealthUrl = "http://localhost:5341/health";
        options.ApiKey = "your-seq-api-key";
    });
```

For finer control over batching or timeouts, tune the native `OtlpExporterOptions` directly — for example
switch the log exporter to batch export:

```csharp
builder.IgniteSeqOpenTelemetryExporter(configureOptions: options =>
{
    options.IngestionEndpoint = "http://localhost:5341";
    options.OtlpLogExporter.ExportProcessorType = ExportProcessorType.Batch;
});
```

> [!NOTE]
> When `OtlpProtocol` is `HttpProtobuf` (the default) and `IngestionEndpoint` is set, the Spark computes the
> per-signal endpoint by appending `/ingest/otlp/v1/logs` and `/ingest/otlp/v1/traces`. Set the exporter
> `Endpoint` yourself only when you use `Grpc` or a non-standard path.

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`SeqOpenTelemetryExporterSpark.ConfigurationSectionPath` (`"Ignite:OpenTelemetry:Exporter:Seq"`). Most apps
never change it; supply a custom path only if you want the Seq config to live elsewhere.

## Health checks

The Spark registers a health check named **`SeqOpenTelemetryExporter`** by default (`HealthChecks.Enabled` is
`true`). The name comes from the public `SeqOpenTelemetryExporterSpark.Name` constant, which also supplies the
built-in health-check tag. For a named registration the name carries the suffix — e.g.
`SeqOpenTelemetryExporter[primary]`. The check performs an HTTP `GET` against the configured `HealthUrl` and
reports healthy on any 2xx response, unhealthy otherwise.

> [!IMPORTANT]
> The health check is registered only when `Settings.Enabled` **and** `HealthChecks.Enabled` are both `true`
> and `HealthUrl` is configured (non-empty). While the Spark is disabled (`Enabled = false`), or when no
> `HealthUrl` is set, no health check is added at all.

The check is tagged with `SeqOpenTelemetryExporterSpark.Name` (`"SeqOpenTelemetryExporter"`), plus any tags
you add via `HealthChecks.Tags`. It surfaces at the health endpoint mapped by `app.Ignite()`.

Disable it via configuration:

```json
{
  "Ignite": {
    "OpenTelemetry": {
      "Exporter": {
        "Seq": {
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
builder.IgniteSeqOpenTelemetryExporter(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

This Spark is itself an **observability exporter** — it does not emit its own telemetry; it forwards the
telemetry Ignite already collects to Seq.

### Tracing

When `Enabled` and `TracesExporterEnabled` are both `true`, the Spark adds an `OtlpTraceExporter` processor to
the Ignite tracer provider. Every activity span produced by your app and the other Sparks (for example the
[Redis](./stackexchange-redis.md) command spans) is exported to Seq. Turn trace export off with
`TracesExporterEnabled = false` while leaving log export on.

### Logging

When `Enabled` and `LogExporterEnabled` are both `true`, the Spark adds an `OtlpLogExporter` processor to the
Ignite logging pipeline. Log records flow to Seq alongside everything else your app logs — including
[Serilog](./serilog.md) output when you enable it. Turn log export off with `LogExporterEnabled = false`.

> [!TIP]
> The `ApiKey` is appended as the `X-Seq-ApiKey` header on both the exported log and trace OTLP requests
> (merged into any headers you already set on `OtlpLogExporter.Headers` / `OtlpTraceExporter.Headers`). Store
> the key as a secret (user-secrets, environment variable, or a Key Vault-backed configuration source) rather
> than in committed `appsettings.json`.

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Serilog Spark](./serilog.md)
- [Redis Spark](./stackexchange-redis.md)
- [Seq documentation](https://docs.datalust.co/docs/getting-started)
- [Seq OpenTelemetry ingestion](https://docs.datalust.co/docs/opentelemetry-net-sdk)
