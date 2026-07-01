---
title: Framework libraries
description: The standalone ES.FX feature libraries — Transactional Outbox and Migrations — and how to use their public APIs.
---

ES.FX ships a small set of standalone feature libraries that solve one recurring problem each and stand
apart from [Ignite](../ignite/index.md). They depend only on the base framework and their own third-party
packages, so you can adopt them in any .NET 10 host — with or without Ignite — and each optionally bridges
into Ignite when you want it to.

## The libraries

| Library | Package(s) | What it gives you |
| --- | --- | --- |
| [**Transactional Outbox**](./transactional-outbox.md) | `ES.FX.TransactionalOutbox` (+ `.EntityFrameworkCore`, provider packages, `.MassTransit`) | Reliable message dispatch tied to your EF Core transaction — enqueue a message in the same `SaveChanges` that writes your data, then a hosted service delivers it. |
| [**Migrations**](./migrations.md) | `ES.FX.Migrations` (+ `ES.FX.Ignite.Migrations`) | A DI-driven migration runner: implement `IMigrationsTask`, register it, and a hosted service applies every task at startup. |

Both libraries are independently consumable. Each has its own page below with the full end-to-end walkthrough.

---

## Transactional Outbox

The [Transactional Outbox](./transactional-outbox.md) library removes the "dual-write" race between your
database and your message broker. Instead of writing to the database and *then* publishing (where a crash
in between loses the message), you enqueue the message **into the same database transaction** that persists
your business data. A background delivery service later reads the stored messages and publishes them —
either both the data and the message commit, or neither does.

Install `ES.FX.TransactionalOutbox.EntityFrameworkCore`, a provider package for your database, and
optionally `.MassTransit` to dispatch onto a bus. Wire the store with `UseOutbox()` / `AddOutbox()`,
enqueue with `dbContext.AddOutboxMessage(...)`, and register the hosted delivery service with
`AddOutboxDeliveryService<TDbContext, TMessageHandler>(...)`.

See the [Transactional Outbox](./transactional-outbox.md) page for the full walkthrough: DbContext wiring,
enqueuing, the delivery service and its tuning options, MassTransit dispatch, custom handlers and fault
handling, and observability.

---

## Migrations

The [Migrations](./migrations.md) library gives you a provider-agnostic way to apply migrations (or any
startup data setup) as part of the host lifecycle. Implement `IMigrationsTask` (one method,
`ApplyMigrations(CancellationToken)`), register it, and the hosted `IgniteMigrationsService` runner
resolves and runs every registered task at startup. `AddDbContextMigrationsTask<TDbContext>` provides a
ready-made task that applies EF Core relational migrations.

See the [Migrations](./migrations.md) page for the full walkthrough: installing and registering the runner,
running EF Core migrations at startup, writing custom tasks, and the `MigrationsServiceSparkSettings`
(`Enabled`, `ExitOnComplete`). The [Migrations Spark](../ignite/sparks/migrations.md) page covers the
runner from the Ignite Spark angle.

---

## See also

- [Transactional Outbox](./transactional-outbox.md)
- [Migrations](./migrations.md)
- [Transactional Outbox — EF Core additions](../additions/entity-framework-core.md)
- [MassTransit additions](../additions/masstransit.md)
- [Entity Framework Core Spark](../ignite/sparks/entity-framework-core.md)
- [Migrations Spark](../ignite/sparks/migrations.md)
- [Ignite overview](../ignite/index.md)
