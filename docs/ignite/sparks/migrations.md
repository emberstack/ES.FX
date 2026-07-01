---
title: Migrations service integration
description: Run all registered IMigrationsTask instances at startup with Ignite, with optional exit-on-complete for migration jobs.
---

## Overview

The Migrations Spark registers a hosted service that runs every registered
[`IMigrationsTask`](../../libraries/migrations.md) once at application startup. Call
`builder.IgniteMigrationsService()` and register one or more migration tasks (for example an EF Core
`DbContext` migration task), and Ignite applies them in sequence when the host starts — timing each task
and, optionally, exiting the process when they finish.

Under the hood the Spark:

- Binds a `MigrationsServiceSparkSettings` from the `Ignite:Services:MigrationsService` configuration
  section.
- Registers a hosted `MigrationsService` that resolves **all** `IMigrationsTask` instances from DI and
  runs their `ApplyMigrations` in registration order at startup.
- Logs the elapsed time for each task and the total, and can call `Environment.Exit(0)` after completion
  when configured for a migration-only job.

> [!IMPORTANT]
> The Spark provides the **runner**, not the migration logic. On its own it runs nothing — you must
> register at least one `IMigrationsTask`. The EF Core Spark ships a ready-made task; see
> [Register a migration task](#register-a-migration-task).

> [!NOTE]
> Unlike most Sparks, the Migrations Spark exposes no service-specific **Options**, no keyed/named
> instances, no health check, and no OpenTelemetry sources. It has only observability-free **Settings**
> (`Enabled`, `ExitOnComplete`). The sections below reflect that reduced surface.

## Install the client

```bash
dotnet add package ES.FX.Ignite.Migrations
```

```xml
<PackageReference Include="ES.FX.Ignite.Migrations" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a project that also centralizes
> versions carries no `Version` attribute. If you install into a standalone consumer, add
> `Version="…"`.

The migration tasks live in their own packages. To migrate an EF Core `DbContext`, also install the EF
Core Spark:

```bash
dotnet add package ES.FX.Ignite.Microsoft.EntityFrameworkCore
```

```xml
<PackageReference Include="ES.FX.Ignite.Microsoft.EntityFrameworkCore" />
```

## Register the client

Call `IgniteMigrationsService` on your host application builder, after `builder.Ignite()`:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Ignite();
builder.IgniteMigrationsService();

var app = builder.Build();
app.Ignite();

await app.RunAsync();
```

The full signature is:

```csharp
public static void IgniteMigrationsService(
    this IHostApplicationBuilder builder,
    Action<MigrationsServiceSparkSettings>? configureSettings = null,
    string configurationSectionPath = MigrationsServiceSpark.ConfigurationSectionPath);
```

> [!NOTE]
> This Spark takes no `name` or `serviceKey` parameters — there is a single `MigrationsService` per host.
> The task list is composed from every `IMigrationsTask` in DI, so you add capacity by registering more
> tasks, not more services.

### What gets registered

| Service | Lifetime | Notes |
| --- | --- | --- |
| `MigrationsService` | Hosted service | Runs all registered `IMigrationsTask` at startup, in registration order. |
| `MigrationsServiceSparkSettings` | Singleton | The resolved settings (`Enabled`, `ExitOnComplete`). |

> [!WARNING]
> Calling `IgniteMigrationsService` twice throws `ReconfigurationNotSupportedException` — the Spark guards
> its configuration key (`MigrationsService`). Register it exactly once.

### Register a migration task

The runner needs something to run. Register one or more `IMigrationsTask` implementations before or after
`IgniteMigrationsService` — order between the two calls does not matter, since tasks are resolved from DI
at startup.

The EF Core Spark provides a relational `DbContext` task via `AddDbContextMigrationsTask<TDbContext>()`,
which applies any pending EF Core migrations for that context:

```csharp
builder.Ignite();
builder.IgniteMigrationsService();

// Registers an IMigrationsTask that calls Database.MigrateAsync() for AppDbContext.
builder.AddDbContextMigrationsTask<AppDbContext>();
```

Register the task once per `DbContext` you want migrated. All registered tasks run sequentially at
startup.

For a custom migration step (seed data, a non-EF store, a script runner), implement `IMigrationsTask`
yourself and add it to DI:

```csharp
public sealed class SeedReferenceDataTask(IServiceProvider services) : IMigrationsTask
{
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        // ... apply your migration / seed logic ...
    }
}

builder.Services.AddTransient<IMigrationsTask, SeedReferenceDataTask>();
```

See [Database migrations](../../libraries/migrations.md) for the `IMigrationsTask` contract in full.

## Configuration

All Migrations service configuration lives under the `Ignite:Services:MigrationsService` section. The
`configureSettings` delegate runs **after** configuration is read from `appsettings.json`, so a delegate
overrides the corresponding JSON values.

### Settings vs options

This Spark has **Settings only** — there is no `Options` type, because the runner needs no service-specific
configuration (the migration logic lives in the tasks). The Settings here are plain toggles rather than the
usual Ignite observability toggles.

| Concept | Type | Purpose | Config location | Customized via |
| --- | --- | --- | --- | --- |
| **Settings** | `MigrationsServiceSparkSettings` | Whether the runner runs, and whether it exits on completion. | `Ignite:Services:MigrationsService:Settings` | `configureSettings` |

`MigrationsServiceSparkSettings` members:

| Member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `true` | When `false`, the hosted service returns immediately and runs no tasks. |
| `ExitOnComplete` | `bool` | `false` | When `true`, the service calls `Environment.Exit(0)` after all tasks complete — useful for a run-migrations-then-quit job. |

> [!WARNING]
> `ExitOnComplete = true` terminates the process via `Environment.Exit(0)` as soon as migrations finish.
> Use it for dedicated migration jobs (for example a Kubernetes init container or one-shot task), **not**
> for a long-running service host that should keep serving after migrating.

### Configure via appsettings

The toggles nest under a `Settings` sub-section of the Spark's root section:

```json
{
  "Ignite": {
    "Services": {
      "MigrationsService": {
        "Settings": {
          "Enabled": true,
          "ExitOnComplete": false
        }
      }
    }
  }
}
```

### Configure with delegates

`configureSettings` runs after `appsettings.json`, so values set here override the JSON above. A common
pattern is to drive `ExitOnComplete` from an environment flag so the same image can run as a migration job
or as a service:

```csharp
builder.IgniteMigrationsService(configureSettings: settings =>
{
    settings.Enabled = true;
    settings.ExitOnComplete =
        builder.Configuration.GetValue<bool>("RUN_MIGRATIONS_ONLY");
});
```

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section. It defaults to
`MigrationsServiceSpark.ConfigurationSectionPath` (`"Ignite:Services:MigrationsService"`). Most apps never
change it; supply a custom path only if you want the settings to live elsewhere.

## Health checks

This Spark registers **no health check**. Migrations run once at startup and the service completes; there
is no ongoing state to probe. Readiness and liveness for the host come from Ignite's core health checks and
any other Sparks you register.

> [!TIP]
> To gate traffic until migrations finish, run them as a separate migration job with
> `ExitOnComplete = true` and start the serving host only after that job succeeds — rather than trying to
> health-check the migration step.

## Observability

### Tracing

This Spark adds no OpenTelemetry `ActivitySource`. The EF Core migration task uses Entity Framework Core's
own instrumentation, so if you have EF Core tracing configured (for example through the
[Entity Framework Core Spark](./entity-framework-core.md)), the underlying `MigrateAsync` calls appear in
your traces through that pipeline.

### Logging

`MigrationsService` logs its progress through the app's configured logging — the number of tasks, each
task's index and elapsed time, the total elapsed time, and whether it will exit on completion. The EF Core
migration task additionally logs the pending-migration count and completion for each `DbContext`. These
flow through the same logging pipeline as the rest of your app — including [Serilog](./serilog.md) when you
enable it — with no extra wiring.

## See also

- [Database migrations](../../libraries/migrations.md)
- [Entity Framework Core Spark](./entity-framework-core.md)
- [Ignite overview](../index.md)
- [Sparks catalog](./index.md)
- [Serilog Spark](./serilog.md)
