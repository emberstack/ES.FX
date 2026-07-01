---
title: Microsoft.AspNetCore additions
description: Small ASP.NET Core middleware components for server-timing, trace-id response headers, and query-string-to-header promotion.
---

## Overview

`ES.FX.Additions.Microsoft.AspNetCore` augments the ASP.NET Core shared framework
(`Microsoft.AspNetCore.App`) with three small, self-contained middleware components. Each one solves a
narrow request-pipeline concern and nothing more:

- **`ServerTimingMiddleware`** — emits a `Server-Timing` response header with the total request duration,
  so browser dev tools and clients can see how long the server took.
- **`TraceIdResponseHeaderMiddleware`** — emits an `X-Trace-Id` response header carrying the current
  distributed-trace id, making it easy to correlate a client-visible request with your traces and logs.
- **`QueryStringToHeaderMiddleware`** — promotes `X-Header-*` query-string parameters into request
  headers, useful for injecting headers from environments where you can only control the URL.

These are plain middleware classes that plug into the standard ASP.NET Core pipeline via
`UseMiddleware<T>()`. The package adds no DI registration helpers, options types, or configuration — it is
deliberately low-opinion.

> [!TIP]
> Using Ignite? You get all three of these middleware wired automatically. `app.Ignite()` inserts
> `ServerTimingMiddleware`, `QueryStringToHeaderMiddleware`, and `TraceIdResponseHeaderMiddleware` into
> the pipeline for `WebApplication` hosts. Reach for this Addition directly only when you want the
> middleware without Ignite, or want to place them yourself. See the [Ignite overview](../ignite/index.md).

## Install

```bash
dotnet add package ES.FX.Additions.Microsoft.AspNetCore
```

```xml
<PackageReference Include="ES.FX.Additions.Microsoft.AspNetCore" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so the in-repo `<PackageReference>` carries no `Version`
> attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

## What it adds

All three types live in the `ES.FX.Additions.Microsoft.AspNetCore.Middleware` namespace.

| Type | Member | Purpose |
| --- | --- | --- |
| `ServerTimingMiddleware` | `Task InvokeAsync(HttpContext context)` | Sets `Server-Timing: total;dur=<ms>` on the response with the total elapsed request time. |
| `TraceIdResponseHeaderMiddleware` | `Task InvokeAsync(HttpContext context)` | Sets `X-Trace-Id` to `Activity.Current?.Id`, falling back to `HttpContext.TraceIdentifier`. |
| `QueryStringToHeaderMiddleware` | `Task InvokeAsync(HttpContext context)` | Copies each `?X-Header-{Name}=value` query parameter into request header `{Name}`. |
| `QueryStringToHeaderMiddleware` | `const string Prefix = "X-Header-"` | The query-string key prefix that marks a parameter for promotion (case-insensitive). |

Each middleware follows the convention-based ASP.NET Core shape: a constructor taking a `RequestDelegate`
and a public `InvokeAsync(HttpContext)` method. Register them with the standard `UseMiddleware<T>()`
extension from ASP.NET Core.

## Usage

### Register the middleware

Add the middleware in `Program.cs` in the order you want them to run. Response-header middleware should
sit early enough to run before the response starts; the header-promotion middleware should sit before any
middleware that reads the promoted headers.

```csharp
using ES.FX.Additions.Microsoft.AspNetCore.Middleware;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Promote ?X-Header-* query parameters into request headers early in the pipeline.
app.UseMiddleware<QueryStringToHeaderMiddleware>();

// Emit response headers for observability.
app.UseMiddleware<ServerTimingMiddleware>();
app.UseMiddleware<TraceIdResponseHeaderMiddleware>();

app.MapGet("/", () => "Hello");

app.Run();
```

### Server-Timing header

`ServerTimingMiddleware` starts a stopwatch when it runs and, via `HttpContext.Response.OnStarting`,
writes the total elapsed milliseconds just before the response is sent:

```text
Server-Timing: total;dur=12.3456
```

Because it registers an `OnStarting` callback, the timing reflects the work done by everything after this
middleware in the pipeline.

### X-Trace-Id header

`TraceIdResponseHeaderMiddleware` writes the current trace id to the response so a client can report the
exact request that failed:

```text
X-Trace-Id: 00-8a3c1f...-b7d4...-01
```

The value is `Activity.Current?.Id` when a distributed-trace `Activity` is active; otherwise it falls
back to `HttpContext.TraceIdentifier`. Pair it with your tracing setup so the id resolves to a real
trace.

### Promote query-string parameters to headers

`QueryStringToHeaderMiddleware` looks for query-string keys that start with the `Prefix` (`X-Header-`,
matched case-insensitively). For each match it strips the prefix and copies the value into a request
header of the remaining name. A request to:

```text
GET /resource?X-Header-Accept-Language=fr-FR
```

results in the request header `Accept-Language: fr-FR` being visible to the rest of the pipeline. Pairs
are silently skipped when the key reduces to an empty name after removing the prefix, when the remaining
name contains characters outside the RFC 9110 header-name token set, or when any value contains a NUL,
CR, or LF character.

> [!WARNING]
> This middleware lets a caller inject arbitrary request headers through the URL. Only enable it where
> that is safe (for example behind a trusted gateway) — never expose it directly to untrusted clients.

## Notes and limitations

- **Middleware only.** The package ships three middleware classes and one `Prefix` constant. It provides
  no `IServiceCollection`/`IApplicationBuilder` extension methods, no options types, and no configuration
  binding — you register each middleware yourself with `UseMiddleware<T>()`.
- **No DI dependencies.** None of the middleware resolve services from DI; they act purely on the
  `HttpContext`.
- **Header names are fixed.** `Server-Timing` and `X-Trace-Id` are not configurable, and the
  query-string `Prefix` is a `const`.
- **Framework reference.** The package references the ASP.NET Core shared framework
  (`Microsoft.AspNetCore.App`); it targets `net10.0` and adds no third-party dependencies.

## See also

- [Ignite overview](../ignite/index.md) — Ignite wires all three middleware into `WebApplication` hosts automatically.
- [Additions](./index.md) — the full catalog of ES.FX Additions.
- [ASP.NET Core middleware](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/) — the upstream middleware pipeline and `UseMiddleware<T>()`.
- [Server-Timing header (MDN)](https://developer.mozilla.org/docs/Web/HTTP/Headers/Server-Timing) — the response header format `ServerTimingMiddleware` emits.
