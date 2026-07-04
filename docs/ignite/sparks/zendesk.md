---
title: Zendesk client integration
description: Register the ES.FX.Zendesk typed client with Ignite, including OAuth configuration binding, a live health check, and OpenTelemetry tracing.
---

## Overview

The Zendesk Spark registers the [ES.FX.Zendesk typed client](../../libraries/zendesk-client.md)
(`IZendeskClient`, OAuth `client_credentials`) into dependency injection, with configuration binding,
startup validation, a live health check, and OpenTelemetry tracing already wired up. Call
`builder.IgniteZendeskClient()` once and inject `IZendeskClient` anywhere in your app.

Under the hood the Spark:

- Binds `ZendeskClientOptions` (subdomain + OAuth credentials) from the `Ignite:Zendesk` configuration
  section with `ValidateOnStart()`, so a bad configuration fails at startup instead of on first use.
- Binds a `ZendeskClientSparkSettings` (observability toggles) from the `Ignite:Zendesk:Settings`
  sub-section.
- Registers the client via the library's `AddZendeskClient()` (typed `HttpClient`, keyed-capable, OAuth
  token provider with cached, single-flight refresh).
- Adds a **live** health check that calls `GET /api/v2/users/me.json` with the configured credentials.
- Adds the client's `ES.FX.Zendesk` `ActivitySource` to the Ignite OpenTelemetry pipeline.

> [!NOTE]
> The client surface, OAuth behavior, error model (`ZendeskApiException`, `RetryAfter`), and the
> resource-area operations are documented on the [Zendesk API client](../../libraries/zendesk-client.md)
> page. This page covers the Ignite wiring.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Zendesk
```

```xml
<PackageReference Include="ES.FX.Ignite.Zendesk" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

## Register the client

Call `IgniteZendeskClient` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();
builder.IgniteZendeskClient();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static IHttpClientBuilder IgniteZendeskClient(
    this IHostApplicationBuilder builder,
    string? name = null,
    string? serviceKey = null,
    Action<ZendeskClientSparkSettings>? configureSettings = null,
    Action<ZendeskClientOptions>? configureOptions = null,
    string configurationSectionPath = ZendeskClientSpark.ConfigurationSectionPath);
```

It returns the underlying `IHttpClientBuilder`, for further customization of the named client.

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `IZendeskClient` | Transient | The typed client. Keyed when `serviceKey` is set. |
| `IZendeskAccessTokenProvider` | Singleton (keyed) | The OAuth token cache for the instance. |
| `ZendeskClientSparkSettings` | Singleton (keyed) | The resolved observability settings. |
| Health check `ZendeskClient` | — | Live credential check. See [Health checks](#health-checks). |
| OpenTelemetry `ActivitySource` | — | `ES.FX.Zendesk`. See [Tracing](#tracing). |

### Consume the client

```csharp
public sealed class TicketLookup(IZendeskClient zendesk)
{
    public Task<ZendeskTicket> GetAsync(long id, CancellationToken cancellationToken) =>
        zendesk.Tickets.GetByIdAsync(id, cancellationToken);
}
```

> [!WARNING]
> Calling `IgniteZendeskClient` twice with the **same** `serviceKey` throws
> `ReconfigurationNotSupportedException`. Register each instance exactly once; to talk to multiple
> Zendesk tenants, give each a distinct `serviceKey` (see below).

### Register keyed clients

To connect to more than one Zendesk tenant, register each as a **keyed** service with a distinct
`serviceKey`, and pass a matching `name` so each reads its own configuration sub-section:

```csharp
builder.IgniteZendeskClient(name: "support", serviceKey: "support");
builder.IgniteZendeskClient(name: "sales", serviceKey: "sales");
```

- **`name`** selects the configuration sub-section: `name: "support"` reads from
  `Ignite:Zendesk:support` instead of `Ignite:Zendesk`. It does not affect DI.
- **`serviceKey`** registers `IZendeskClient` as a **keyed** service. Resolve it with
  `[FromKeyedServices("…")]`. When `serviceKey` is `null`, the client is the default (unkeyed)
  registration.

```json
{
  "Ignite": {
    "Zendesk": {
      "support": {
        "Subdomain": "acme-support",
        "OAuth": { "ClientId": "…" }
      },
      "sales": {
        "Subdomain": "acme-sales",
        "OAuth": { "ClientId": "…" }
      }
    }
  }
}
```

## Configuration

All Zendesk configuration lives under the `Ignite:Zendesk` section. Both delegates
(`configureSettings`, `configureOptions`) run **after** configuration is read from `appsettings.json`,
so a delegate overrides the corresponding JSON values.

### Settings vs options

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Options** | `ZendeskClientOptions` | The Zendesk account + OAuth credentials. | `Ignite:Zendesk` | `configureOptions` |
| **Settings** | `ZendeskClientSparkSettings` | Ignite observability toggles. | `Ignite:Zendesk:Settings` | `configureSettings` |

`ZendeskClientOptions` (`Subdomain`, `BaseUrl`, `OAuth.ClientId` / `ClientSecret` / `Scope` /
`ExpiresIn` / `ExpiryBuffer` / `TokenEndpoint`) is documented in full on the
[client library page](../../libraries/zendesk-client.md#configuration). Options are validated at
startup: a missing subdomain, malformed base URL, or missing OAuth credentials stops the host with a
clear message.

`ZendeskClientSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HealthChecks.Enabled` | `bool` | `true` | Registers the live Zendesk health check. |
| `HealthChecks.Timeout` | `TimeSpan?` | none | Timeout applied to the health check. |
| `HealthChecks.FailureStatus` | `HealthStatus?` | `Unhealthy` | Reported status when the check fails. |
| `HealthChecks.Tags` | `string[]` | `[]` | Extra tags added alongside the built-in `Zendesk` tag. |
| `Tracing.Enabled` | `bool` | `true` | Adds the `ES.FX.Zendesk` tracing source. |

> [!NOTE]
> The Zendesk Spark exposes no `Metrics` setting — HTTP-level metrics come from .NET's built-in
> `http.client.*` instruments, which Ignite's OpenTelemetry pipeline already collects.

### Configure via appsettings

Account and OAuth values sit at the section root; the observability toggles nest under a `Settings`
sub-section:

```json
{
  "Ignite": {
    "Zendesk": {
      "Subdomain": "acme",
      "OAuth": {
        "ClientId": "acme-integration",
        "Scope": "read"
      },
      "Settings": {
        "HealthChecks": { "Enabled": true },
        "Tracing": { "Enabled": true }
      }
    }
  }
}
```

> [!WARNING]
> Do not commit `OAuth:ClientSecret` to source control. Supply it via user secrets or an environment
> variable — `Ignite__Zendesk__OAuth__ClientSecret` — or a secret store such as the
> [Azure Key Vault Secrets Spark](./azure-keyvault-secrets.md).

### Configure with delegates

```csharp
builder.IgniteZendeskClient(
    configureSettings: settings =>
    {
        settings.HealthChecks.Timeout = TimeSpan.FromSeconds(5);
        settings.Tracing.Enabled = true;
    },
    configureOptions: options =>
    {
        options.Subdomain = "acme";
        options.OAuth.Scope = "read";
    });
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`ZendeskClientSpark.ConfigurationSectionPath` (`"Ignite:Zendesk"`). Most apps never change it.

## Health checks

The Spark registers a health check named **`ZendeskClient`** by default (`HealthChecks.Enabled` is
`true`). For a keyed registration the name carries the key suffix — e.g. `ZendeskClient[support]`. The
check is **live**: it calls `GET /api/v2/users/me.json`, which verifies DNS, TLS, the OAuth token flow,
and the credentials in one probe. A healthy result reports the authenticated user id and role — never
the email, since health output can surface on an unauthenticated `/health` endpoint.

The check is tagged `Zendesk`, plus any tags you add via `HealthChecks.Tags`. It surfaces at the health
endpoints mapped by `app.Ignite()`.

> [!NOTE]
> Each probe spends one real Zendesk API request, which counts against the tenant's rate limit. With
> aggressive probe intervals, budget accordingly or point only the readiness probe at it.

Disable it via configuration:

```json
{
  "Ignite": {
    "Zendesk": {
      "Settings": {
        "HealthChecks": { "Enabled": false }
      }
    }
  }
}
```

Or via the delegate:

```csharp
builder.IgniteZendeskClient(configureSettings: settings =>
    settings.HealthChecks.Enabled = false);
```

## Observability

### Tracing

Tracing is enabled by default (`Tracing.Enabled` is `true`). The Spark adds the client's
`ES.FX.Zendesk` `ActivitySource` to the Ignite OpenTelemetry pipeline, so every Zendesk operation
appears as a `Zendesk.{Area}.{Operation}` client span — with the underlying HTTP span from Ignite's
`HttpClient` instrumentation as its child.

Disable it via configuration (`Ignite:Zendesk:Settings:Tracing:Enabled = false`) or the
`configureSettings` delegate.

### Resilience

Ignite applies the standard resilience handler to **every** `HttpClient` by default
(`Ignite:HttpClient:StandardResilienceHandlerEnabled`), including this one — transient failures and
`429 Too Many Requests` are retried honoring `Retry-After`, with no Spark-specific wiring. When
retries are exhausted, the thrown `ZendeskApiException` still carries the last `RetryAfter` hint.

### Logging

The client logs through the app's configured logging pipeline — including
[Serilog](./serilog.md) when you enable it — with no extra wiring: `Debug` on success, `Warning` with
the status code on failure. Response bodies are never logged.

## See also

- [Zendesk API client](../../libraries/zendesk-client.md) — the client surface, OAuth model, and error handling.
- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Azure Key Vault Secrets Spark](./azure-keyvault-secrets.md) — for the OAuth client secret.
- [Zendesk API reference](https://developer.zendesk.com/api-reference/)
