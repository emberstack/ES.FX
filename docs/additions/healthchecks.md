---
title: Health Checks additions
description: An HTTP GET health check that reports healthy when a URI returns a success status code.
---

## Overview

`ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks` augments
[Microsoft.Extensions.Diagnostics.HealthChecks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
with a ready-made `IHealthCheck` implementation: `HttpGetHealthCheck`. It issues an HTTP `GET` to a URI
and reports **healthy** when the response has a success status code; otherwise it reports the
registration's configured `FailureStatus` (**unhealthy** by default).

Reach for it when you need to gate readiness/liveness on an external HTTP endpoint (a downstream API, a
sidecar, an exporter's health URL) without hand-writing an `IHealthCheck`. It fills the small gap between
the health-check abstractions and the common "is this URL up?" probe.

> [!NOTE]
> This package builds only on `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions`. For the base
> health-check registration API (`AddHealthChecks()`, `HealthCheckRegistration`, tags, failure status),
> see the [upstream documentation](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks).

## Install

```bash
dotnet add package ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks
```

```xml
<PackageReference Include="ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so the in-repo `<PackageReference>` carries no `Version`
> attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

## What it adds

| Type | Signature | Purpose |
| --- | --- | --- |
| `HttpGetHealthCheck` | `HttpGetHealthCheck(HttpGetHealthCheckOptions options) : IHealthCheck` | Health check that GETs `options.Uri` and returns `Healthy` on a success status code, or the registration's `FailureStatus` otherwise. Throws `ArgumentNullException` if `options` is `null`. |
| `HttpGetHealthCheckOptions` | `class { required string Uri { get; set; } TimeSpan? Timeout { get; set; } }` | The URI to probe (`Uri` is required) and an optional per-attempt timeout. |

Both types live in the
`ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks.Http` namespace.

> [!NOTE]
> The check evaluates `HttpResponseMessage.IsSuccessStatusCode` (2xx). Any non-success status returns
> the registration's configured `FailureStatus` with the description `HTTP GET returned {statusCode}`.
> A transport failure (DNS failure, connection refused, etc.) or a per-attempt timeout is caught and
> reported as the `FailureStatus` with the exception attached, while cancellation of the ambient
> `CancellationToken` propagates out of `CheckHealthAsync`.

## Usage

Register the check with the standard health-checks builder using `AddCheck`:

```csharp
using ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks.Http;

builder.Services.AddHealthChecks()
    .AddCheck("upstream-api", new HttpGetHealthCheck(new HttpGetHealthCheckOptions
    {
        Uri = "https://api.example.com/health"
    }));
```

To resolve the URI from configuration or options at check time, register it as a
`HealthCheckRegistration` with a factory so the URI is read when the check runs:

```csharp
using ES.FX.Additions.Microsoft.Extensions.Diagnostics.HealthChecks.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

builder.Services.AddHealthChecks()
    .Add(new HealthCheckRegistration(
        name: "upstream-api",
        factory: sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<MyOptions>>().CurrentValue;
            return new HttpGetHealthCheck(new HttpGetHealthCheckOptions { Uri = options.HealthUrl });
        },
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]));
```

Tag the registration with `"live"` if you want it to gate liveness under Ignite; untagged checks gate
readiness only. See [Ignite health check endpoints](../ignite/index.md) for how those tags map to the
`/health` endpoints.

## Notes and limitations

- **Success is 2xx only.** The check uses `IsSuccessStatusCode`; it does not follow custom "healthy"
  status rules, inspect the body, or treat redirects as healthy.
- **Transport failures are caught.** An `HttpRequestException` (DNS failure, refused connection) or a
  timeout-induced `TaskCanceledException` is caught inside the check, which returns the registration's
  `FailureStatus` with the exception attached. Ambient cancellation (the caller's `CancellationToken`)
  is not caught and propagates out of the check.
- **Shared static `HttpClient`.** All instances share a single static `HttpClient` (with
  distributed-trace header propagation disabled and a pooled-connection lifetime of 2 minutes), and the
  probe reads only the response headers (`ResponseHeadersRead`). It does not participate in
  `IHttpClientFactory` or Ignite's resilience-configured clients.
- **Optional per-attempt timeout.** `HttpGetHealthCheckOptions.Timeout` cancels the request after the
  given duration and reports the registration's `FailureStatus`; when `null`, only the ambient
  cancellation token is honored. The health-check registration's `timeout` argument (an upstream
  feature) can still apply an overall bound.
- **No DI extension method.** This package intentionally ships just the `IHealthCheck` and its options —
  you register it through the standard `AddCheck` / `HealthCheckRegistration` surface. It is used
  internally by the [Seq exporter Spark](../ignite/sparks/seq-exporter.md) to probe Seq's health URL.

## See also

- [Microsoft.Extensions.Diagnostics.HealthChecks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks) — the upstream health-check API.
- [Ignite overview](../ignite/index.md) — how Ignite maps readiness/liveness endpoints and health-check tags.
- [Seq OpenTelemetry exporter Spark](../ignite/sparks/seq-exporter.md) — a Spark that uses this check for its health probe.
- [Additions](./index.md) — the full Additions catalog.
