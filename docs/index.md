---
title: ES.FX
description: A collection of reusable .NET 10 libraries published as ES.FX.* NuGet packages, from core primitives to the Ignite application bootstrap.
---

ES.FX (EmberStack Framework) is a collection of reusable .NET libraries and application
frameworks, published as `ES.FX.*` NuGet packages. Its flagship is **Ignite** — an
opinionated, "just add water" bootstrap that wires OpenTelemetry, health checks, HttpClient
resilience, and service integrations from a single call.

## What is ES.FX

ES.FX is a set of independently consumable .NET 10 libraries. Each package targets `net10.0`
and lives under the `ES.FX.*` namespace. Take only the layer you need: pick a single core
primitive, add a focused helper for a third-party library, or adopt the full Ignite bootstrap
for a new service — the layers stack, but none forces the ones above it on you.

Everything is delivered as NuGet packages from NuGet.org and GitHub Packages. If you are
consuming ES.FX in an app, start with [Getting started](./getting-started/index.md). If you
are contributing to the framework itself, start with the [Development guide](./development/index.md).

## The layers

ES.FX is organized in five layers. Dependencies point downward — an upper layer may build on a
lower one, never the reverse — so you can adopt any layer in isolation.

| Layer | What it is | Start here |
| --- | --- | --- |
| **Core** (`ES.FX`) | Framework-agnostic primitives: `Result`/`Problem` error handling, `Optional<T>`, `DurationValue`, `ValueRange`, and BCL-style extensions. | [Results & Problems](./development/results-and-problems.md), [Primitives](./development/primitives.md) |
| **Additions** (`ES.FX.Additions.*`) | Focused, low-opinion helpers — each package augments exactly **one** third-party dependency and nothing else. | [Additions catalog](./additions/index.md) |
| **Hosting** (`ES.FX.Hosting`) | `ProgramEntry` / `ProgramEntryBuilder` — a lifecycle wrapper around `Main` with structured startup, error handling, and graceful shutdown. | [Application hosting](./development/hosting.md) |
| **Ignite** (`ES.FX.Ignite` + Sparks) | The opinionated bootstrap and its pluggable **Sparks** — self-contained service integrations that bind config, register DI, add health checks, and wire OpenTelemetry. | [Ignite overview](./ignite/index.md) |
| **Feature libraries** | Standalone libraries independent of Ignite: **Transactional Outbox**, **Migrations**, and the **Zendesk API client**. | [Framework libraries](./libraries/index.md) |

## Meet Ignite

Ignite activates in **two phases** around the build boundary: `builder.Ignite(...)` runs on the
`IHostApplicationBuilder` before the host is built, and `app.Ignite()` runs on the built `IHost`.
Between them you register Sparks — one line each — to plug services into the host.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Ignite();            // Phase A: OpenTelemetry, health checks, resilience, config
builder.IgniteRedisClient(); // add a Spark — registers IConnectionMultiplexer, health check, tracing

var app = builder.Build();

app.Ignite();                // Phase B: middleware + health-check endpoints (web hosts)
await app.RunAsync();
```

> [!NOTE]
> `builder.Ignite(...)` (on `IHostApplicationBuilder`) and `app.Ignite()` (on `IHost`) are two
> distinct calls. Web-only middleware wired in Phase B applies only to `WebApplication` hosts;
> worker and console hosts skip it and simply get no web middleware.

For the full walkthrough, see the [Quickstart](./getting-started/quickstart.md) and the
[Ignite overview](./ignite/index.md).

## Choose your path

| I want to… | Go to |
| --- | --- |
| Install ES.FX and stand up my first Ignite app | [Getting started](./getting-started/index.md) → [Quickstart](./getting-started/quickstart.md) |
| Understand the vocabulary (Ignite, Sparks, Settings vs Options) | [Core concepts](./getting-started/concepts.md) |
| Plug a service into Ignite | [Sparks catalog](./ignite/sparks/index.md) |
| Add a helper for a specific third-party library | [Additions catalog](./additions/index.md) |
| Use the core error-handling and primitive types | [Results & Problems](./development/results-and-problems.md), [Primitives](./development/primitives.md) |
| Use a standalone feature library | [Transactional Outbox](./libraries/transactional-outbox.md), [Migrations](./libraries/migrations.md), [Zendesk client](./libraries/zendesk-client.md) |
| Expose Zendesk to an AI agent (MCP server) | [Zendesk MCP server](./libraries/zendesk-mcp-server.md) |
| Build, test, or contribute to the framework | [Development guide](./development/index.md) |

## See also

- [Getting started](./getting-started/index.md) — install the packages and bootstrap an app.
- [Ignite overview](./ignite/index.md) — the two-phase model and what Ignite gives you.
- [Sparks catalog](./ignite/sparks/index.md) — every available service integration.
- [Additions catalog](./additions/index.md) — focused helpers for third-party libraries.
- [Development guide](./development/index.md) — build, test, and contribute to ES.FX.
