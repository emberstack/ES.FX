---
title: Entity Framework Core additions
description: Helpers for EF Core that run ModelBuilder configuration from options, create DbContexts from a delegate, and provide a Testcontainers design-time factory.
---

Two focused packages augment [Entity Framework Core](https://learn.microsoft.com/ef/core/) without pulling in Ignite:

- **`ES.FX.Additions.Microsoft.EntityFrameworkCore`** — provider-agnostic helpers on top of `Microsoft.EntityFrameworkCore`.
- **`ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime`** — a SQL Server design-time factory built on `Microsoft.EntityFrameworkCore.SqlServer` and Testcontainers.

They add plumbing that plain EF Core makes awkward: injecting `ModelBuilder` configuration through `DbContextOptions` (so a shared component can contribute mappings to a `DbContext` it doesn't own), building a `DbContext` from an arbitrary delegate, and spinning up a real SQL Server for design-time operations like `dotnet ef migrations`.

> [!TIP]
> Using Ignite? The [Entity Framework Core Spark](../ignite/sparks/entity-framework-core.md) registers a `DbContext` with health checks and tracing already wired. Reach for these Additions when you want the raw helpers without the Ignite bootstrap.

## Overview

Base EF Core assumes each `DbContext` configures its own model in `OnModelCreating` and is constructed by the DI container or a compiled `IDbContextFactory<TContext>`. These Additions fill two gaps that show up in modular applications:

- **Model configuration from options.** A cross-cutting feature (for example, the Transactional Outbox) needs to add tables to a `DbContext` defined in another assembly. `ModelBuilderConfigureExtension` carries `Action<ModelBuilder, DbContextOptions>` callbacks on the `DbContextOptions` themselves, and `ModelBuilder.ConfigureFromExtension(options)` replays them during `OnModelCreating`.
- **Delegate-based construction.** `DelegateDbContextFactory<TDbContext>` implements `IDbContextFactory<TDbContext>` over a plain factory function, so you can build a context however you like (custom state, a resolved connection, test doubles) while still satisfying code that depends on the factory abstraction.

The SqlServer.DesignTime package adds `TestContainerDesignTimeFactory<TDbContext>`, an `IDesignTimeDbContextFactory<TDbContext>` that starts a throwaway SQL Server container so the EF Core design-time tools have a real database to talk to.

## Install

`ES.FX.Additions.Microsoft.EntityFrameworkCore`:

```bash
dotnet add package ES.FX.Additions.Microsoft.EntityFrameworkCore
```

```xml
<PackageReference Include="ES.FX.Additions.Microsoft.EntityFrameworkCore" />
```

`ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime` (add only if you need the design-time factory; it references the core package transitively):

```bash
dotnet add package ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime
```

```xml
<PackageReference Include="ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so an in-repo `<PackageReference>` carries no `Version` attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

## What it adds

### `ES.FX.Additions.Microsoft.EntityFrameworkCore`

| Member | Signature | Purpose |
| --- | --- | --- |
| `BuilderExtensions.WithConfigureModelBuilderExtension` | `void WithConfigureModelBuilderExtension(this DbContextOptionsBuilder builder, params Action<ModelBuilder, DbContextOptions>[] configureActions)` | Attaches model-configuration callbacks to the `DbContextOptions` being built. Call it while configuring options; repeated calls on the same builder append to the previously registered callbacks. |
| `BuilderExtensions.ConfigureFromExtension` | `void ConfigureFromExtension(this ModelBuilder modelBuilder, DbContextOptions options)` | Runs the callbacks stored by `WithConfigureModelBuilderExtension`. Call it from `OnModelCreating`. A no-op if none were registered. |
| `ModelBuilderConfigureExtension` | `class ModelBuilderConfigureExtension(params Action<ModelBuilder, DbContextOptions>[] configureActions) : IDbContextOptionsExtension` | The `IDbContextOptionsExtension` that carries the callbacks. Usually created for you by `WithConfigureModelBuilderExtension`; exposes `ConfigureActions`. |
| `DelegateDbContextFactory<TDbContext>` | `class DelegateDbContextFactory<TDbContext>(IServiceProvider serviceProvider, Func<IServiceProvider, TDbContext> factory) : IDbContextFactory<TDbContext> where TDbContext : DbContext` | An `IDbContextFactory<TDbContext>` whose `CreateDbContext()` invokes `factory(serviceProvider)`. |

### `ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime`

| Member | Signature | Purpose |
| --- | --- | --- |
| `TestContainerDesignTimeFactory<TDbContext>` | `class TestContainerDesignTimeFactory<TDbContext> : IDesignTimeDbContextFactory<TDbContext> where TDbContext : DbContext` | Design-time factory that starts a Testcontainers SQL Server and returns a `TDbContext` bound to it. Override the `protected virtual` hooks below to customize. |

`TestContainerDesignTimeFactory<TDbContext>` exposes these overridable hooks and constants:

- `protected virtual void ConfigureMsSqlContainerBuilder(MsSqlBuilder builder)` — customize the container before it starts.
- `protected virtual void ConfigureDbContextOptionsBuilder(DbContextOptionsBuilder<TDbContext> builder)` — customize options before the provider is applied.
- `protected virtual void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder builder)` — customize the SQL Server provider (for example, migrations assembly).
- `static readonly string Registry` / `static readonly string Image` / `static readonly string Tag` — the container image coordinates (`mcr.microsoft.com`, `mssql/server`, `2025-latest`). Exposed as `static readonly` (not `const`) so a corrected value in a newer package version reaches already-compiled consumers.

## Usage

### Contribute model configuration through options

Register callbacks while building the `DbContextOptions`, then replay them inside `OnModelCreating`. This lets a component that does not own the `DbContext` still add mappings to it.

```csharp
using ES.FX.Additions.Microsoft.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

// When configuring options (e.g. in AddDbContext):
services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer(connectionString);

    // Any component can attach model configuration here.
    options.WithConfigureModelBuilderExtension((modelBuilder, dbContextOptions) =>
    {
        modelBuilder.Entity<AuditEntry>().ToTable("__AuditLog");
    });
});
```

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private readonly DbContextOptions<AppDbContext> _options = options;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Replay every callback registered via WithConfigureModelBuilderExtension.
        modelBuilder.ConfigureFromExtension(_options);
    }
}
```

> [!NOTE]
> `ConfigureFromExtension` reads the callbacks from `DbContextOptions`, so any assembly holding a reference to those options can add mappings. If no `ModelBuilderConfigureExtension` was registered, the call does nothing.

> [!WARNING]
> **The EF Core model is cached per `DbContext` type, not per options instance.** `ModelBuilderConfigureExtension.ExtensionInfo` reports a service-provider hash code of `0` and always returns `true` from `ShouldUseSameServiceProvider`. Because of this, two options instances of the **same** `DbContext` type that carry **different** `WithConfigureModelBuilderExtension` callbacks are treated as equivalent by EF Core and share a single cached model. Whichever set of callbacks builds the model first wins; the second instance's callbacks are **silently never run**.
>
> If you need per-instance model variation (the same `DbContext` type configured with different mappings in different scopes), supply a custom [`IModelCacheKeyFactory`](https://learn.microsoft.com/ef/core/modeling/dynamic-model) that folds the registered configure actions (or another discriminator) into the model cache key, so EF Core builds and caches a distinct model per configuration.

### Build a `DbContext` from a delegate

Use `DelegateDbContextFactory<TDbContext>` when you need `IDbContextFactory<TDbContext>` but want full control over how the context is constructed.

```csharp
using ES.FX.Additions.Microsoft.EntityFrameworkCore.Factories;
using Microsoft.EntityFrameworkCore;

IDbContextFactory<AppDbContext> factory = new DelegateDbContextFactory<AppDbContext>(
    serviceProvider,
    sp =>
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AppDbContext(options);
    });

await using var dbContext = factory.CreateDbContext();
```

### Provide a design-time SQL Server for EF tooling

Derive from `TestContainerDesignTimeFactory<TDbContext>` so `dotnet ef` (migrations, scaffolding) has a real SQL Server to connect to without a running database. The EF Core tools discover the `IDesignTimeDbContextFactory<TDbContext>` automatically.

```csharp
using ES.FX.Additions.Microsoft.EntityFrameworkCore.SqlServer.DesignTime;
using Microsoft.EntityFrameworkCore.Infrastructure;

public sealed class AppDbContextDesignTimeFactory
    : TestContainerDesignTimeFactory<AppDbContext>
{
    // Point migrations at the assembly that holds them (optional).
    protected override void ConfigureSqlServerOptions(SqlServerDbContextOptionsBuilder builder)
        => builder.MigrationsAssembly(typeof(AppDbContextDesignTimeFactory).Assembly.FullName);
}
```

```bash
dotnet ef migrations add InitialCreate
```

> [!IMPORTANT]
> `TestContainerDesignTimeFactory<TDbContext>` starts a Docker container via Testcontainers, so a running Docker engine is required wherever the design-time tools execute. It is intended for design-time and test scenarios, not for application runtime.

## Notes and limitations

- **Provider-agnostic core.** `ES.FX.Additions.Microsoft.EntityFrameworkCore` depends only on `Microsoft.EntityFrameworkCore` and `Microsoft.Extensions.DependencyInjection`. It adds no runtime services, DI registrations, health checks, or observability — for that, use the [Entity Framework Core Spark](../ignite/sparks/entity-framework-core.md).
- **You wire the replay.** `WithConfigureModelBuilderExtension` only stores callbacks; nothing runs until you call `ConfigureFromExtension` from `OnModelCreating`. Forgetting the replay silently drops the configuration.
- **Model caching ignores the callbacks.** EF Core caches one model per `DbContext` type, and `ModelBuilderConfigureExtension` reports a service-provider hash of `0` with `ShouldUseSameServiceProvider => true`. Two options instances of the same `DbContext` type carrying different callbacks therefore share one cached model, so the second set of callbacks silently never runs. For per-instance model variation, supply a custom `IModelCacheKeyFactory` — see the [warning above](#contribute-model-configuration-through-options).
- **SqlServer.DesignTime package is design-time focused.** `TestContainerDesignTimeFactory<TDbContext>` targets the EF Core design-time workflow and tests; it uses `Testcontainers.MsSql` and requires Docker. It is not a runtime `DbContext` factory.
- **Base EF Core API unchanged.** These helpers sit alongside the standard EF Core surface — `UseSqlServer`, `AddDbContext`, migrations, and `OnModelCreating` all behave exactly as documented upstream. See the [EF Core documentation](https://learn.microsoft.com/ef/core/) for the base API.

## See also

- [Entity Framework Core Spark](../ignite/sparks/entity-framework-core.md) — full Ignite wiring (DI, health checks, tracing) for a `DbContext`.
- [Migrations](../libraries/migrations.md) — the DI-driven migration runner that pairs with EF Core.
- [Transactional Outbox](../libraries/transactional-outbox.md) — captures messages in the same EF Core transaction and uses model configuration extensions.
- [Additions catalog](./index.md) — the full list of Additions.
- [EF Core documentation](https://learn.microsoft.com/ef/core/) and [design-time DbContext creation](https://learn.microsoft.com/ef/core/cli/dbcontext-creation) — the upstream base APIs.
