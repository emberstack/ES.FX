---
title: Migrations
description: Run database migrations at startup with the IMigrationsTask abstraction and the DI-driven Ignite migrations runner.
---

## Overview

Migrations gives you a small, DI-driven way to apply database (or any other) migrations when your
application starts. It has three moving parts:

- **`IMigrationsTask`** (`ES.FX.Migrations`) — the single-method abstraction. Implement it for anything
  that needs to be "brought up to date" at startup.
- **`IgniteMigrationsService`** (`ES.FX.Ignite.Migrations`) — a hosted service that resolves **all**
  registered `IMigrationsTask` instances from DI and runs them sequentially, timing each one.
- **`AddDbContextMigrationsTask<TDbContext>`** (`ES.FX.Ignite.Microsoft.EntityFrameworkCore`) — the
  ready-made EF Core task that applies pending migrations for a `DbContext`. It is the concrete task the
  runner picks up, so you rarely implement `IMigrationsTask` by hand.

The core abstraction has no dependency on Ignite; the runner and the EF Core task are the Ignite-side
glue. Reach for this when you want migrations applied automatically on boot, or run as a dedicated
migration-only job that exits when finished.

## Install

The abstraction lives in `ES.FX.Migrations`:

```bash
dotnet add package ES.FX.Migrations
```

```xml
<PackageReference Include="ES.FX.Migrations" />
```

To run tasks at startup with Ignite, also add the runner and (for EF Core) the EF Core Spark:

```bash
dotnet add package ES.FX.Ignite.Migrations
dotnet add package ES.FX.Ignite.Microsoft.EntityFrameworkCore
```

```xml
<PackageReference Include="ES.FX.Ignite.Migrations" />
<PackageReference Include="ES.FX.Ignite.Microsoft.EntityFrameworkCore" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` entries in this repository carry no
> `Version` attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

## The `IMigrationsTask` abstraction

A migrations task is a single asynchronous method:

```csharp
namespace ES.FX.Migrations.Abstractions;

public interface IMigrationsTask
{
    Task ApplyMigrations(CancellationToken cancellationToken = default);
}
```

Implement it for any startup-time work that must run before the app serves traffic — applying EF Core
migrations, seeding reference data, running a schema tool, and so on:

```csharp
using ES.FX.Migrations.Abstractions;

public sealed class SeedReferenceDataTask(MyDbContext context) : IMigrationsTask
{
    public async Task ApplyMigrations(CancellationToken cancellationToken = default)
    {
        if (!await context.Currencies.AnyAsync(cancellationToken))
        {
            context.Currencies.AddRange(Currency.Defaults);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
```

Register it as `IMigrationsTask` in DI so the runner discovers it:

```csharp
builder.Services.AddTransient<IMigrationsTask, SeedReferenceDataTask>();
```

## Run tasks with the Ignite migrations service

`builder.IgniteMigrationsService(...)` registers a hosted `MigrationsService`. At startup it resolves
**every** `IMigrationsTask` registered in DI and calls `ApplyMigrations` on each in registration order,
logging the elapsed time per task and in total.

```csharp
public static void IgniteMigrationsService(
    this IHostApplicationBuilder builder,
    Action<MigrationsServiceSparkSettings>? configureSettings = null,
    string configurationSectionPath = MigrationsServiceSpark.ConfigurationSectionPath)
```

Wire it up between `builder.Ignite()` and `builder.Build()`, then register the tasks it should run:

```csharp
builder.Ignite();

builder.IgniteMigrationsService();

// Register a DbContext factory (EF Core Spark), then the EF migrations task for it.
builder.IgniteSqlServerDbContextFactory<SimpleDbContext>(nameof(SimpleDbContext));
builder.AddDbContextMigrationsTask<SimpleDbContext>();

var app = builder.Build();
app.Ignite();
await app.RunAsync();
```

> [!IMPORTANT]
> The runner only runs tasks that are registered as `IMigrationsTask` in DI. If nothing is registered,
> the service starts, finds zero tasks, and completes immediately. Register your tasks (via
> `AddDbContextMigrationsTask<T>` or your own `AddTransient<IMigrationsTask, …>`) **before** `Build()`.

> [!WARNING]
> `IgniteMigrationsService` calls `builder.GuardConfigurationKey(...)`. Calling it twice on the same
> builder throws `ReconfigurationNotSupportedException`. Register the service once.

### The EF Core migrations task

`AddDbContextMigrationsTask<TDbContext>()` registers the ready-made
`RelationalDbContextMigrationsTask<TDbContext>` as an `IMigrationsTask`. At startup it checks
`GetPendingMigrationsAsync` for the context and calls `MigrateAsync` when there is anything to apply —
so it is a no-op when the database is already current.

```csharp
public static void AddDbContextMigrationsTask<TDbContext>(this IHostApplicationBuilder builder)
    where TDbContext : DbContext
```

```csharp
builder.AddDbContextMigrationsTask<SimpleDbContext>();
```

This is the normal way to feed the runner: register a `DbContext` (see the
[Entity Framework Core Spark](../ignite/sparks/entity-framework-core.md)), add its migrations task, and
`IgniteMigrationsService` applies the pending migrations at startup.

> [!TIP]
> The task resolves `TDbContext` from DI, so the context must be registered (for example via
> `IgniteSqlServerDbContextFactory<TDbContext>`). Point EF Core at the assembly that holds your
> migrations with `MigrationsAssembly(...)` when they live in a separate project.

## Configuration

The runner is an Ignite Spark, so its settings live under the `Ignite:` configuration root. The default
section is `MigrationsServiceSpark.ConfigurationSectionPath`, which resolves to
`Ignite:Services:MigrationsService`. Settings bind from the `:Settings` sub-node of that section, and the
`configureSettings` delegate runs **after** configuration is read, so it overrides `appsettings.json`.

`MigrationsServiceSparkSettings` has two toggles:

| Setting | Type | Default | Purpose |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `true` | When `false`, the hosted service starts but runs no tasks. |
| `ExitOnComplete` | `bool` | `false` | When `true`, the process calls `Environment.Exit(0)` after all tasks finish — use for a dedicated migration-only job. |

### Configure via appsettings

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

### Configure with a delegate

```csharp
builder.IgniteMigrationsService(configureSettings: settings =>
{
    settings.Enabled = true;
    settings.ExitOnComplete = true; // run migrations, then exit the process
});
```

> [!WARNING]
> With `ExitOnComplete = true` the runner calls `Environment.Exit(0)` as soon as the tasks finish. This
> terminates the whole process immediately — intended for a migration-only container or job, not for a
> long-running app that should keep serving requests.

### Configuration section path

The final `configurationSectionPath` parameter overrides the root section the settings bind from.
Its default is `MigrationsServiceSpark.ConfigurationSectionPath` (`Ignite:Services:MigrationsService`).
Most applications never change it.

## Run as a migration-only job

Because the runner is just a hosted service driven by DI, the same wiring works in any host — an
ASP.NET app, a background worker, or a console host. For a dedicated migration job, set
`ExitOnComplete = true` so the process runs the tasks and exits:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Ignite();
builder.IgniteMigrationsService(configureSettings: s => s.ExitOnComplete = true);

builder.IgniteSqlServerDbContextFactory<SimpleDbContext>(nameof(SimpleDbContext));
builder.AddDbContextMigrationsTask<SimpleDbContext>();

var app = builder.Build();
app.Ignite();
await app.RunAsync();
```

## See also

- [Migrations Spark](../ignite/sparks/migrations.md) — the Spark reference for `IgniteMigrationsService`.
- [Entity Framework Core Spark](../ignite/sparks/entity-framework-core.md) — registers the `DbContext`
  that `AddDbContextMigrationsTask<T>` migrates.
- [Transactional Outbox](./transactional-outbox.md) — another standalone feature library, often paired
  with EF Core.
- [Ignite overview](../ignite/index.md) — the two-phase `builder.Ignite()` / `app.Ignite()` model.
