---
title: Hermes Agent client integration
description: Register the ES.FX.NousResearch.HermesAgent typed client with Ignite, including configuration binding, a live health check, and OpenTelemetry tracing.
---

## Overview

The Hermes Agent Spark registers the
[ES.FX.NousResearch.HermesAgent typed client](../../libraries/hermes-agent-client.md)
(`IHermesAgentClient`, static bearer-key authentication) into dependency injection, with configuration
binding, startup validation, a live health check, and OpenTelemetry tracing already wired up. Call
`builder.IgniteHermesAgentClient()` once and inject `IHermesAgentClient` anywhere in your app.

Under the hood the Spark:

- Binds `HermesAgentClientOptions` (`BaseUrl` + `ApiKey`) from the `Ignite:NousResearch:HermesAgent`
  configuration section with `ValidateOnStart()`, so a bad configuration fails at startup instead of on
  first use.
- Binds a `HermesAgentClientSparkSettings` (observability toggles) from the
  `Ignite:NousResearch:HermesAgent:Settings` sub-section.
- Registers the client via the library's `AddHermesAgentClient()` (typed `HttpClient`, keyed-capable,
  bearer-key authentication handler — no OAuth machinery).
- Adds a **live** health check that calls `GET /v1/capabilities` with the configured API key.
- Adds the client's `ES.FX.NousResearch.HermesAgent` `ActivitySource` to the Ignite OpenTelemetry
  pipeline.

> [!NOTE]
> The client surface (chat, responses, runs, jobs, sessions, server discovery), streaming with
> `await foreach`, and the error model (`HermesAgentApiException`) are documented on the
> [Hermes Agent API client](../../libraries/hermes-agent-client.md) page. This page covers the Ignite
> wiring.

## Install the client

```bash
dotnet add package ES.FX.Ignite.NousResearch.HermesAgent
```

```xml
<PackageReference Include="ES.FX.Ignite.NousResearch.HermesAgent" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteHermesAgentClient` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteHermesAgentClient();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static IHttpClientBuilder IgniteHermesAgentClient(
    this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<HermesAgentClientSparkSettings>? configureSettings = null,
    Action<HermesAgentClientOptions>? configureOptions = null,
    string configurationSectionPath = HermesAgentClientSpark.ConfigurationSectionPath);
```

It returns the underlying `IHttpClientBuilder`, for further customization of the named client.

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `IHermesAgentClient` | Transient | The typed client. Keyed when `serviceKey` is set. |
| `HermesAgentClientSparkSettings` | Singleton (keyed) | The resolved observability settings. |
| Health check `HermesAgentClient` | — | Live API-key check. See [Health checks](#health-checks). |
| OpenTelemetry `ActivitySource` | — | `ES.FX.NousResearch.HermesAgent`. See [Tracing](#tracing). |

### Consume the client

```csharp
public sealed class AgentGateway(IHermesAgentClient hermes)
{
    public Task<HermesAgentChatCompletion> AskAsync(HermesAgentChatCompletionRequest request,
        CancellationToken cancellationToken) =>
        hermes.Chat.CompleteAsync(request, cancellationToken: cancellationToken);
}
```

> [!WARNING]
> Calling `IgniteHermesAgentClient` twice with the **same** `serviceKey` throws
> `ReconfigurationNotSupportedException`. Register each instance exactly once; to talk to multiple
> Hermes Agent servers, give each a distinct `serviceKey` (see below).

### Register keyed clients

To connect to more than one Hermes Agent server, register each as a **keyed** service with a distinct
`serviceKey`, and pass a matching `name` so each reads its own configuration sub-section:

```csharp
builder.IgniteHermesAgentClient(name: "research", serviceKey: "research");
builder.IgniteHermesAgentClient(name: "coding", serviceKey: "coding");
```

- **`name`** selects the configuration sub-section: `name: "research"` reads from
  `Ignite:NousResearch:HermesAgent:research` instead of `Ignite:NousResearch:HermesAgent`. It does not
  affect DI.
- **`serviceKey`** registers `IHermesAgentClient` as a **keyed** service. Resolve it with
  `[FromKeyedServices("…")]`. When `serviceKey` is `null`, the client is the default (unkeyed)
  registration.

```json
{
  "Ignite": {
    "NousResearch": {
      "HermesAgent": {
        "research": {
          "BaseUrl": "http://hermes-research:8642"
        },
        "coding": {
          "BaseUrl": "http://hermes-coding:8642"
        }
      }
    }
  }
}
```

## Configuration

All Hermes Agent configuration lives under the `Ignite:NousResearch:HermesAgent` section. Both
delegates (`configureSettings`, `configureOptions`) run **after** configuration is read from
`appsettings.json`, so a delegate overrides the corresponding JSON values.

### Settings vs options

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Options** | `HermesAgentClientOptions` | The server address + API key. | `Ignite:NousResearch:HermesAgent` | `configureOptions` |
| **Settings** | `HermesAgentClientSparkSettings` | Ignite observability toggles. | `Ignite:NousResearch:HermesAgent:Settings` | `configureSettings` |

`HermesAgentClientOptions` members:

| Member | Type | Required | Purpose |
| --- | --- | --- | --- |
| `BaseUrl` | `string?` | Yes | Absolute http(s) URL of the Hermes Agent API server (e.g. `http://localhost:8642`). A trailing `/` is appended automatically so relative paths compose. |
| `ApiKey` | `string?` | Yes | The static server key, sent as `Authorization: Bearer {ApiKey}` on every request. |

Options are validated at startup: a missing or malformed `BaseUrl`, or a missing `ApiKey`, stops the
host with a clear message. The full options reference lives on the
[client library page](../../libraries/hermes-agent-client.md#configuration).

`HermesAgentClientSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the live Hermes Agent health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `HermesAgent` tag. |
| `Tracing.Enabled` | `bool` | `true` | Adds the `ES.FX.NousResearch.HermesAgent` tracing source. |

> [!NOTE]
> The Hermes Agent Spark exposes no `Metrics` setting — HTTP-level metrics come from .NET's built-in
> `http.client.*` instruments, which Ignite's OpenTelemetry pipeline already collects.

### Configure via appsettings

The server address and API key sit at the section root; the observability toggles nest under a
`Settings` sub-section:

```json
{
  "Ignite": {
    "NousResearch": {
      "HermesAgent": {
        "BaseUrl": "http://localhost:8642",
        "Settings": {
          "HealthChecks": { "Enabled": true },
          "Tracing": { "Enabled": true }
        }
      }
    }
  }
}
```

> [!WARNING]
> Do not commit `ApiKey` to source control. Supply it via user secrets or an environment variable —
> `Ignite__NousResearch__HermesAgent__ApiKey` — or a secret store such as the
> [Azure Key Vault Secrets Spark](./azure-keyvault-secrets.md). For keyed instances the sub-section
> joins the path: `Ignite__NousResearch__HermesAgent__research__ApiKey`.

### Configure with delegates

```csharp
builder.IgniteHermesAgentClient(
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
        settings.Tracing.Enabled = true;
    },
    configureOptions: options =>
    {
        options.BaseUrl = "http://localhost:8642";
    });
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`HermesAgentClientSpark.ConfigurationSectionPath` (`"Ignite:NousResearch:HermesAgent"`). Most apps
never change it.

## Health checks

The Spark registers a health check named **`HermesAgentClient`** by default (`HealthChecks.Enabled` is
`true`). For a keyed registration the name carries the key suffix — e.g. `HermesAgentClient[research]`.
The check is **live**: it calls `GET /v1/capabilities`, an authenticated endpoint, which verifies DNS,
TLS, server reachability, and the configured API key in one probe. A healthy result reports the
server's platform and model — nothing sensitive, since health output can surface on an unauthenticated
`/health` endpoint.

The check is tagged `HermesAgent`, plus any tags you add via `HealthChecks.Tags`. It surfaces at the
health endpoints mapped by `app.Ignite()`.

> [!NOTE]
> Each probe spends one real, authenticated Hermes Agent API request. With aggressive probe intervals,
> budget accordingly or point only the readiness probe at it.

Disable it via configuration:

```json
{
  "Ignite": {
    "NousResearch": {
      "HermesAgent": {
        "Settings": {
          "HealthChecks": { "Enabled": false }
        }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteHermesAgentClient(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark adds the client's
`ES.FX.NousResearch.HermesAgent` `ActivitySource` to the Ignite OpenTelemetry pipeline, so every
Hermes Agent operation appears as a `HermesAgent.{Area}.{Operation}` client span — with the underlying
HTTP span from Ignite's `HttpClient` instrumentation as its child.

Disable it via configuration (`Ignite:NousResearch:HermesAgent:Settings:Tracing:Enabled = false`) or
the `configureSettings` delegate.

### Resilience

Ignite applies the standard resilience handler to **every** `HttpClient` by default
(`Ignite:Settings:HttpClient:StandardResilienceHandlerEnabled`), including this one — transient failures and
`429 Too Many Requests` are retried honoring `Retry-After`, with no Spark-specific wiring. When
retries are exhausted, the guard still throws a `HermesAgentApiException` carrying the final status
code.

> [!NOTE]
> The standard handler's total-request timeout also governs long-lived **streaming** calls
> (`Chat.StreamAsync`, run event streams, session chat streams). If your agent runs stream for longer
> than that budget, tune or disable the handler for this client via the returned `IHttpClientBuilder`.

### Logging

The client logs through the app's configured logging pipeline — including
[Serilog](./serilog.md) when you enable it — with no extra wiring: `Debug` on success, `Warning` with
the status code on failure. Response bodies are never logged.

## See also

- [Hermes Agent API client](../../libraries/hermes-agent-client.md) — the client surface, streaming, and error handling.
- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Azure Key Vault Secrets Spark](./azure-keyvault-secrets.md) — for the API key.
