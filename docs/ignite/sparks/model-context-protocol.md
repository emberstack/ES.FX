---
title: Model Context Protocol (MCP) server integration
description: Register a Model Context Protocol server with Ignite, including Streamable HTTP transport, execution-mode guards, and OpenTelemetry tracing and metrics.
---

## Overview

The Model Context Protocol (MCP) Spark hosts an [MCP](https://modelcontextprotocol.io/) server inside
your Ignite application, built on the
[`ModelContextProtocol.AspNetCore`](https://github.com/modelcontextprotocol/csharp-sdk) SDK. Call
`builder.IgniteModelContextProtocolServer()` to register the server with the Streamable HTTP transport,
execution-mode (read-only / dry-run) support, and OpenTelemetry tracing and metrics already wired up —
then map its endpoints post-build with `app.IgniteModelContextProtocol()`.

Under the hood the Spark:

- Binds a `ModelContextProtocolSparkOptions` (transport and execution-mode configuration) and a
  `ModelContextProtocolSparkSettings` (observability toggles) from the `Ignite:ModelContextProtocol`
  configuration section.
- Registers an MCP server via `AddMcpServer().WithHttpTransport(...)`, returning the underlying
  `IMcpServerBuilder` so you can register your own tools, prompts, and resources.
- Registers an `IMcpExecutionModeAccessor` so tools can enforce a read-only or dry-run baseline, optionally
  tightened per request by a header.
- Adds the SDK's OpenTelemetry `ActivitySource` and `Meter` so MCP activity appears in your traces and
  metrics.

> [!NOTE]
> Unlike most Sparks, `IgniteModelContextProtocolServer` returns an `IMcpServerBuilder` (not `void`), and
> the Spark registers **no** DI client for you to inject. You use the returned builder to attach your
> tools/prompts/resources. There is also **no** `name` or `serviceKey` parameter — a host runs a single MCP
> server.

## Install the client

```bash
dotnet add package ES.FX.Ignite.ModelContextProtocol
```

```xml
<PackageReference Include="ES.FX.Ignite.ModelContextProtocol" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

The MCP Spark is two-part: register the server pre-build, then map its endpoints post-build.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();

builder.IgniteModelContextProtocolServer()
    .WithTools<WeatherTools>();   // register your own tools/prompts/resources

var app = builder.Build();
app.Ignite();

app.IgniteModelContextProtocol();   // maps the MCP endpoints — call after app.Ignite()

await app.RunAsync();
```

> [!IMPORTANT]
> `IgniteModelContextProtocol()` extends `WebApplication` and must be called **after** `app.Ignite()`, so
> the MCP endpoints sit behind Ignite's middleware. On non-web hosts (worker/console) there is no
> `WebApplication` to map endpoints onto, so the MCP transport does not apply.

The pre-build registration signature is:

```csharp
public static IMcpServerBuilder IgniteModelContextProtocolServer(
    this IHostApplicationBuilder builder,
    Action<ModelContextProtocolSparkSettings>? configureSettings = null,
    Action<ModelContextProtocolSparkOptions>? configureOptions = null,
    string configurationSectionPath = ModelContextProtocolSpark.ConfigurationSectionPath);
```

The post-build mapping signature is:

```csharp
public static WebApplication IgniteModelContextProtocol(this WebApplication app);
```

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| MCP server (`IMcpServerBuilder`) | — | Registered via `AddMcpServer().WithHttpTransport(...)`. The builder is returned to you to attach tools/prompts/resources. |
| `IMcpExecutionModeAccessor` | Singleton | Resolves the effective execution mode for the current request. See [Enforce an execution mode](#enforce-an-execution-mode). |
| `ModelContextProtocolSparkSettings` | Singleton | The resolved observability settings. |
| `IHttpContextAccessor` | Singleton | Added so the execution-mode accessor can read the per-request override header. |
| OpenTelemetry `ActivitySource` / `Meter` | — | `Experimental.ModelContextProtocol`. See [Observability](#observability). |

> [!WARNING]
> Calling `IgniteModelContextProtocolServer` twice on the same builder throws
> `ReconfigurationNotSupportedException`. Register the MCP server exactly once.

### Attach tools, prompts, and resources

`IgniteModelContextProtocolServer` returns the `IMcpServerBuilder` from the MCP SDK, so you register your
capabilities with the SDK's own fluent API:

```csharp
builder.IgniteModelContextProtocolServer()
    .WithTools<WeatherTools>()
    .WithPrompts<SupportPrompts>();
```

See the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) for the tool/prompt/resource
registration surface (`WithTools`, `WithPrompts`, `WithResources`, and the `[McpServerTool]` attributes).

### Enforce an execution mode

The Spark ships a read-only / dry-run guard so a tool can decide whether a mutating operation may proceed.
Inject `IMcpExecutionModeAccessor` and branch on the effective mode:

```csharp
public sealed class WeatherTools(IMcpExecutionModeAccessor executionMode)
{
    [McpServerTool]
    public string UpdateForecast(string city, string forecast)
    {
        var mode = executionMode.EffectiveMode;

        if (mode.IsReadOnly())
            throw new InvalidOperationException("Write operations are not permitted in read-only mode.");

        if (mode.IsDryRun())
            return $"[dry-run] Would update {city}.";

        // mode.AllowsWrites() is true — perform the real write.
        SaveForecast(city, forecast);
        return $"Updated {city}.";
    }
}
```

`EffectiveMode` is the configured baseline (`ModelContextProtocolSparkOptions.Mode`) possibly **tightened**
by a per-request header (see [Execution-mode configuration](#execution-mode)). It is never less restrictive
than `ConfiguredMode`. The `McpExecutionModeExtensions` helpers — `AllowsWrites()`, `IsDryRun()`,
`IsReadOnly()` — read the mode without repeating the enum comparisons.

## Configuration

All MCP configuration lives under the `Ignite:ModelContextProtocol` section. Both delegates
(`configureSettings`, `configureOptions`) run **after** configuration is read from `appsettings.json`, so a
delegate overrides the corresponding JSON values.

### Settings vs options

The Spark splits configuration into two types with two separate delegates:

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Options** | `ModelContextProtocolSparkOptions` | The MCP transport and execution-mode configuration. | `Ignite:ModelContextProtocol` | `configureOptions` |
| **Settings** | `ModelContextProtocolSparkSettings` | Ignite observability toggles. | `Ignite:ModelContextProtocol:Settings` | `configureSettings` |

`ModelContextProtocolSparkOptions` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `Endpoint` | `string` | `""` | The route pattern the MCP endpoints are mapped to. Empty maps them at the application root. |
| `Stateless` | `bool` | `true` | When `true` the Streamable HTTP transport runs statelessly (no `Mcp-Session-Id`, horizontally scalable). Server-to-client requests (sampling, elicitation, roots) are unavailable in this mode. |
| `Mode` | `McpExecutionMode` | `Default` | The baseline execution mode enforced by the server (`Default`, `DryRun`, or `ReadOnly`). |
| `AllowModeHeaderOverride` | `bool` | `true` | When `true`, a per-request header may further **tighten** the execution mode (never relax it). |
| `ModeHeaderName` | `string` | `"X-Mcp-Execution-Mode"` | The request header used to request a more restrictive execution mode. |

`ModelContextProtocolSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `Tracing.Enabled` | `bool` | `true` | Adds the MCP SDK tracing source. |
| `Metrics.Enabled` | `bool` | `true` | Adds the MCP SDK metrics meter. |

> [!NOTE]
> The MCP Spark exposes no `HealthChecks` setting — it registers no health check (see
> [Health checks](#health-checks)).

### Execution mode

`Mode` sets the baseline the server enforces:

| `McpExecutionMode` | Effect |
| --- | --- |
| `Default` | All operations execute normally; writes perform their changes. |
| `DryRun` | Writes are accepted and report success but make no changes; reads execute normally. |
| `ReadOnly` | Writes are rejected; only reads are permitted. |

When `AllowModeHeaderOverride` is `true`, a client may send the `ModeHeaderName` header (default
`X-Mcp-Execution-Mode`) to request a more restrictive mode for a single request. The header value is
case-insensitive and tolerant of separators — `read-only`, `readonly`, `read_only`, `dry-run`, `dryrun`,
`default`, and `normal` all parse. A request can only ever **tighten** the mode, never relax it: an
unrecognized value, or a value less restrictive than the baseline, yields the configured baseline.

### Configure via appsettings

`Endpoint`, `Stateless`, and the execution-mode options sit at the section root; the observability toggles
nest under a `Settings` sub-section:

```json
{
  "Ignite": {
    "ModelContextProtocol": {
      "Endpoint": "/mcp",
      "Stateless": true,
      "Mode": "ReadOnly",
      "AllowModeHeaderOverride": true,
      "ModeHeaderName": "X-Mcp-Execution-Mode",
      "Settings": {
        "Tracing": { "Enabled": true },
        "Metrics": { "Enabled": true }
      }
    }
  }
}
```

### Configure with delegates

`configureSettings` and `configureOptions` are separate delegates. Both run after `appsettings.json`, so
values set here override the JSON above:

```csharp
builder.IgniteModelContextProtocolServer(
    configureSettings: settings =>
    {
        settings.Tracing.Enabled = true;
        settings.Metrics.Enabled = true;
    },
    configureOptions: options =>
    {
        options.Endpoint = "/mcp";
        options.Stateless = true;
        options.Mode = McpExecutionMode.ReadOnly;
    })
    .WithTools<WeatherTools>();
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`ModelContextProtocolSpark.ConfigurationSectionPath` (`"Ignite:ModelContextProtocol"`). Most apps never
change it; supply a custom path only if you want the MCP config to live somewhere else.

## Health checks

This Spark registers **no** health check — there is no `HealthChecks` setting on
`ModelContextProtocolSparkSettings`. The MCP endpoints are reachable through the standard readiness and
liveness endpoints that `app.Ignite()` maps, but the Spark contributes no check of its own.

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark adds the
`Experimental.ModelContextProtocol` `ActivitySource` (from `McpTelemetry.ActivitySourceName`) to the Ignite
OpenTelemetry pipeline, so MCP server activity appears as spans in your traces.

> [!NOTE]
> The `Experimental.` prefix is version-sensitive and expected to change in a future MCP SDK release. ES.FX
> centralizes it in `McpTelemetry` so it only needs to be updated in one place.

Disable it via configuration:

```json
{
  "Ignite": {
    "ModelContextProtocol": {
      "Settings": {
        "Tracing": { "Enabled": false }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteModelContextProtocolServer(configureSettings: settings =>
    settings.Tracing.Enabled = false);
```

### Metrics

Metrics are enabled by default (`Metrics.Enabled` is `true`). The Spark adds the
`Experimental.ModelContextProtocol` `Meter` (from `McpTelemetry.MeterName`) to the Ignite OpenTelemetry
pipeline. Disable it via `Settings.Metrics.Enabled` in `appsettings.json` or in the `configureSettings`
delegate:

```csharp
builder.IgniteModelContextProtocolServer(configureSettings: settings =>
    settings.Metrics.Enabled = false);
```

### Logging

The MCP server logs through the app's configured logging pipeline — including
[Serilog](./serilog.md) when you enable it — with no extra wiring.

> [!TIP]
> To ship spans and metrics somewhere, configure an OpenTelemetry exporter through Ignite — for example the
> [Seq exporter Spark](./seq-exporter.md).

## See also

- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Serilog Spark](./serilog.md)
- [Seq exporter Spark](./seq-exporter.md)
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
