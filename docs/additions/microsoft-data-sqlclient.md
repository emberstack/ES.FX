---
title: Microsoft.Data.SqlClient additions
description: A SqlConnection factory abstraction and a connection-liveness probe layered on top of Microsoft.Data.SqlClient.
---

## Overview

`ES.FX.Additions.Microsoft.Data.SqlClient` augments the official
[Microsoft.Data.SqlClient](https://learn.microsoft.com/sql/connect/ado-net/microsoft-ado-net-sql-server)
driver with two small, dependency-injection-friendly building blocks:

- **`ISqlConnectionFactory`** — a factory abstraction for producing `SqlConnection` instances, so
  consumers depend on an interface rather than newing up connections against a hard-coded connection
  string. A ready-made `DelegateSqlConnectionFactory` adapts any `Func<IServiceProvider, SqlConnection>`
  into that interface.
- **`SqlServerSafeQuery`** — extension methods that run a cheap `SELECT 1` against a `SqlConnection` to
  verify it is reachable, returning a `bool` instead of throwing. This is the primitive behind
  connection health checks.

The package stays intentionally thin: it adds no configuration, no hosted services, and no observability.
It gives you a testable seam over connection creation plus a liveness probe, and nothing more.

> [!TIP]
> Using Ignite? The [SQL Server client Spark](../ignite/sparks/microsoft-data-sqlclient.md) registers
> an `ISqlConnectionFactory` for you with a health check and tracing already wired up. Reach for this
> Addition directly only when you want the helpers without Ignite.

## Install

```bash
dotnet add package ES.FX.Additions.Microsoft.Data.SqlClient
```

```xml
<PackageReference Include="ES.FX.Additions.Microsoft.Data.SqlClient" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so the in-repo `<PackageReference>` carries no `Version`
> attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

## What it adds

| Type / member | Signature | Purpose |
| --- | --- | --- |
| `ISqlConnectionFactory.CreateConnection` | `SqlConnection CreateConnection()` | Create a new `SqlConnection` synchronously. |
| `ISqlConnectionFactory.CreateConnectionAsync` | `Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)` | Create a new `SqlConnection` asynchronously. Default implementation wraps `CreateConnection()`. |
| `DelegateSqlConnectionFactory` | `DelegateSqlConnectionFactory(IServiceProvider serviceProvider, Func<IServiceProvider, SqlConnection> factory)` | `ISqlConnectionFactory` that produces connections from a delegate, passing the `IServiceProvider`. |
| `SqlServerSafeQuery.CommandText` | `const string CommandText = "SELECT 1"` | The probe command text used by the safe-query helpers. |
| `SqlServerSafeQuery.ExecuteSafeQuery` | `bool ExecuteSafeQuery(this SqlConnection connection, bool close = true)` | Opens the connection (if not already open), runs `SELECT 1`, and returns whether it succeeded. Returns `false` on any failure instead of throwing. |
| `SqlServerSafeQuery.ExecuteSafeQueryAsync` | `Task<bool> ExecuteSafeQueryAsync(this SqlConnection connection, bool close = true, CancellationToken cancellationToken = default)` | Async form of `ExecuteSafeQuery`. Returns `false` on any failure, except caller-requested cancellation, which propagates as `OperationCanceledException`. |

The types live in these namespaces:

- `ES.FX.Additions.Microsoft.Data.SqlClient.Abstractions` — `ISqlConnectionFactory`
- `ES.FX.Additions.Microsoft.Data.SqlClient.Factories` — `DelegateSqlConnectionFactory`
- `ES.FX.Additions.Microsoft.Data.SqlClient.Queries` — `SqlServerSafeQuery`

## Usage

### Register a connection factory

Register `DelegateSqlConnectionFactory` as the `ISqlConnectionFactory` implementation. The delegate
receives the `IServiceProvider`, so you can pull a connection string (or `SqlConnectionStringBuilder`)
from configuration or any other registered service.

```csharp
using ES.FX.Additions.Microsoft.Data.SqlClient.Abstractions;
using ES.FX.Additions.Microsoft.Data.SqlClient.Factories;
using Microsoft.Data.SqlClient;

builder.Services.AddSingleton<ISqlConnectionFactory>(sp =>
    new DelegateSqlConnectionFactory(sp, provider =>
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        return new SqlConnection(configuration.GetConnectionString("Sql"));
    }));
```

Consumers then depend on the interface, which keeps connection creation swappable and mockable in tests:

```csharp
public sealed class OrderReader(ISqlConnectionFactory connectionFactory)
{
    public async Task<int> CountAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.Orders";
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
```

### Probe a connection for liveness

`ExecuteSafeQuery` / `ExecuteSafeQueryAsync` open the connection (if it is not already open), run
`SELECT 1`, and return a `bool`. They return `false` on failure instead of throwing, so they are safe to
call from a health check or a readiness probe without a `try/catch`. The one exception is cancellation:
if the `CancellationToken` you pass to `ExecuteSafeQueryAsync` is canceled, the resulting
`OperationCanceledException` propagates to the caller rather than being reported as `false`.

```csharp
using ES.FX.Additions.Microsoft.Data.SqlClient.Queries;

await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

var isReachable = await connection.ExecuteSafeQueryAsync(cancellationToken: cancellationToken);
if (!isReachable)
{
    // The database did not answer SELECT 1 — treat it as unavailable.
}
```

By default the helper closes the connection after the probe (`close: true`). Pass `close: false` to keep
it open if you intend to reuse the same `SqlConnection` immediately afterward.

```csharp
if (connection.ExecuteSafeQuery(close: false))
{
    // Connection is open and verified — continue using it.
}
```

## Notes and limitations

- **No DI registration helpers.** The package ships the `ISqlConnectionFactory` abstraction and
  `DelegateSqlConnectionFactory` implementation, but does not add an `AddSqlConnectionFactory(...)`
  extension. Register the factory yourself, or use the
  [SQL Server client Spark](../ignite/sparks/microsoft-data-sqlclient.md) for the full Ignite wiring.
- **`ExecuteSafeQuery` reports failure as `false`.** A failed probe — unreachable server, bad
  credentials, timeout — returns `false`, not an exception. It is a liveness signal, not a way to surface
  the underlying error; log or diagnose connectivity separately if you need the cause. The only
  exceptions that escape are `ArgumentNullException` for a `null` connection and, on the async overload,
  `OperationCanceledException` when the caller's token is canceled.
- **You own the connection lifetime.** The factory hands back a `SqlConnection`; disposing it (for
  example with `await using`) is the caller's responsibility. The safe-query helpers only manage the
  open/close state governed by the `close` parameter, not disposal.
- **Scope is deliberately narrow.** This Addition augments `Microsoft.Data.SqlClient` only. For the base
  connection, command, and parameter APIs, see the upstream documentation.

## See also

- [SQL Server client Spark](../ignite/sparks/microsoft-data-sqlclient.md) — the same factory wired into
  Ignite with a health check and tracing.
- [Additions overview](./index.md) — what Additions are and how they differ from Sparks.
- [Entity Framework Core additions](./entity-framework-core.md) — helpers for the EF Core stack when you
  want an ORM rather than raw ADO.NET.
- [Microsoft.Data.SqlClient documentation](https://learn.microsoft.com/sql/connect/ado-net/microsoft-ado-net-sql-server) —
  the upstream driver API.
