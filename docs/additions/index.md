---
title: Additions
description: Focused, low-opinion helpers that each augment exactly one third-party library, with no Ignite dependency.
---

## What are Additions

Additions are small, focused helper packages. Each one augments **exactly one** third-party
dependency — nothing more. The package name tells you what it augments: `ES.FX.Additions.FluentValidation`
adds helpers for FluentValidation, `ES.FX.Additions.StackExchange.Redis` adds helpers for
StackExchange.Redis, and so on.

Additions are deliberately **low-opinion**. They add convenience types, extension methods, converters,
and small building blocks that the upstream library leaves out. They do not take over your composition
root, they do not force a configuration model on you, and they pull in no framework beyond the single
dependency they extend. Reference one, use the parts you want, ignore the rest.

Every Addition follows the same shape as the rest of the framework: it targets `net10.0`, ships as an
`ES.FX.*` NuGet package, and depends only on its one upstream library (plus the core `ES.FX` package when
it needs `Result`/`Problem`). Take only the Additions you need.

## When to use an Addition vs a Spark

Additions and [Sparks](../ignite/sparks/index.md) both help you work with third-party libraries, but they
sit at different layers:

- An **Addition** gives you raw helpers with **no Ignite dependency**. You wire the library into DI
  yourself; the Addition just makes that library nicer to use. Reach for an Addition when you are **not**
  using Ignite, or when you want a specific helper without adopting the full integration.
- A **Spark** wires the library into [Ignite](../ignite/index.md) end to end: configuration binding, DI
  registration, health checks, and OpenTelemetry — all from one `builder.Ignite{Service}...()` call.
  Reach for a Spark when you **are** using Ignite and want the full "just add water" experience.

> [!TIP]
> Several libraries have **both** an Addition and a Spark (Redis, FluentValidation, Entity Framework
> Core, Serilog, and more). If you are on Ignite, prefer the Spark — it uses the matching Addition under
> the hood and adds observability for free. Each Addition page links to its paired Spark where one exists.

## Catalog

Every Addition, grouped by function. Each links to its dedicated page.

### Validation

| Addition | Augments | Purpose |
| --- | --- | --- |
| [FluentValidation](./fluentvalidation.md) | `FluentValidation` | Helpers that bridge validation results into `Result`/`Problem`. |

### Messaging

| Addition | Augments | Purpose |
| --- | --- | --- |
| [MassTransit](./masstransit.md) | `MassTransit` (+ `.MediatR`, `.RabbitMQ`) | Message-kind routing, entity/endpoint name formatters, and transport helpers. |
| [MediatR](./mediatr.md) | `MediatR` (+ `.Contracts`) | Helpers and shared contracts for building MediatR pipelines. |

### Data & EF Core

| Addition | Augments | Purpose |
| --- | --- | --- |
| [Entity Framework Core](./entity-framework-core.md) | `Microsoft.EntityFrameworkCore` (+ `.SqlServer`) | DbContext and model-building helpers, with SqlServer-specific extras. |
| [Microsoft.Data.SqlClient](./microsoft-data-sqlclient.md) | `Microsoft.Data.SqlClient` | Helpers for working with the SQL Server client and connections. |

### ASP.NET Core & APIs

| Addition | Augments | Purpose |
| --- | --- | --- |
| [Microsoft.AspNetCore](./microsoft-aspnetcore.md) | `Microsoft.AspNetCore` | Middleware building blocks (Server-Timing, query-string-to-header, trace-id response header). |
| [API versioning](./asp-versioning.md) | `Asp.Versioning` | Helpers for configuring API versioning. |
| [NSwag.AspNetCore](./nswag-aspnetcore.md) | `NSwag.AspNetCore` | Helpers for wiring NSwag OpenAPI generation and UI. |
| [Health checks](./healthchecks.md) | `Microsoft.Extensions.Diagnostics.HealthChecks` | Helpers for defining and composing health checks. |
| [Identity Core](./identity-core.md) | `Microsoft.Extensions.Identity.Core` | Helpers for ASP.NET Core Identity primitives. |

### Serialization

| Addition | Augments | Purpose |
| --- | --- | --- |
| [System.Text.Json](./system-text-json.md) | `System.Text.Json` | Extra converters (Unix-time dates, flexible booleans) and serializer helpers. |
| [Newtonsoft.Json](./newtonsoft-json.md) | `Newtonsoft.Json` | A contract resolver and helpers for Json.NET. |
| [OneOf](./oneof.md) | `OneOf` | Ready-made discriminated-union result types and `Problem` interop. |

### Caching

| Addition | Augments | Purpose |
| --- | --- | --- |
| [StackExchange.Redis](./stackexchange-redis.md) | `StackExchange.Redis` | Helpers for working with the Redis multiplexer and connections. |

### Logging

| Addition | Augments | Purpose |
| --- | --- | --- |
| [Serilog](./serilog.md) | `Serilog` | Bootstrap logging helpers, including `ProgramEntryBuilder.UseSerilog()`. |

### Infrastructure

| Addition | Augments | Purpose |
| --- | --- | --- |
| [KubernetesClient](./kubernetesclient.md) | `KubernetesClient` | Helpers for configuring and using the Kubernetes client. |

## See also

- [Sparks catalog](../ignite/sparks/index.md) — the Ignite integrations, several of which pair with an Addition.
- [Ignite overview](../ignite/index.md) — the opinionated bootstrap Additions plug into.
- [Framework libraries](../libraries/index.md) — the standalone feature libraries.
- [Results and Problems](../development/results-and-problems.md) — the core error primitives several Additions build on.
