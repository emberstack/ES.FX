---
title: Transactional Outbox
description: Capture messages inside your EF Core transaction and dispatch them reliably with a background delivery service.
---

## Overview

The Transactional Outbox library implements the [outbox pattern](https://microservices.io/patterns/data/transactional-outbox.html):
you persist outbound messages in the **same database transaction** that changes your business data, then a
background service delivers them afterwards. Because the message and the state change commit atomically,
you never publish a message for a transaction that rolled back, and you never lose a message for a
transaction that committed.

The library is split into small, composable packages so you take only what you need:

| Package | Role |
| --- | --- |
| `ES.FX.TransactionalOutbox` | Core contracts: entities, delivery/fault handlers, serialization, observability. |
| `ES.FX.TransactionalOutbox.EntityFrameworkCore` | Capture (`UseOutbox` / `AddOutbox` / `AddOutboxMessage`) and the hosted delivery service. |
| `ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer` | SQL Server-optimized delivery provider. |
| `ES.FX.TransactionalOutbox.EntityFrameworkCore.PostgreSql` | PostgreSQL-optimized delivery provider. |
| `ES.FX.TransactionalOutbox.EntityFrameworkCore.MySql` | MySQL/MariaDB-optimized delivery provider. Requires MySQL 8.0+ or MariaDB 10.6+ (`FOR UPDATE SKIP LOCKED`). |
| `ES.FX.TransactionalOutbox.MassTransit` | `MassTransitOutboxMessageHandler` that publishes captured messages onto a MassTransit bus. |

This library is independent of Ignite — it plugs into any host with an EF Core `DbContext` and DI.

> [!NOTE]
> Two moving parts make up a working outbox: **capture** (writing messages into the outbox tables as part
> of your transaction) and **delivery** (a background service that reads and dispatches them). You configure
> them separately.

## Install

Install the EF Core package (capture + delivery), a database provider package, and — if you dispatch onto
a message bus — the MassTransit package.

```bash
dotnet add package ES.FX.TransactionalOutbox.EntityFrameworkCore
dotnet add package ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer
dotnet add package ES.FX.TransactionalOutbox.MassTransit
```

```xml
<PackageReference Include="ES.FX.TransactionalOutbox.EntityFrameworkCore" />
<PackageReference Include="ES.FX.TransactionalOutbox.EntityFrameworkCore.SqlServer" />
<PackageReference Include="ES.FX.TransactionalOutbox.MassTransit" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` entries in this repository carry no
> `Version` attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

The core `ES.FX.TransactionalOutbox` package is pulled in transitively by the EF Core package; reference it
directly only when you implement your own handlers against its contracts.

## Capture messages

Capture happens on your EF Core `DbContext`. There are three pieces: wire the outbox extension into the
`DbContextOptionsBuilder`, map the outbox tables in the model, and enqueue messages within a transaction.

### Configure the DbContext

Call `UseOutbox` when building the context options, and `AddOutbox` in `OnModelCreating` to map the
`__Outboxes` and `__OutboxMessages` tables:

```csharp
public class SimpleDbContext(DbContextOptions<SimpleDbContext> options) : DbContext(options)
{
    public required DbSet<SimpleUser> SimpleUsers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseOutbox();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox();
    }
}
```

`UseOutbox` accepts an optional `Action<OutboxDbContextOptions>` if you need to replace the default
serializer (`Serializer`, an `IOutboxSerializer` — defaults to a `System.Text.Json`-based serializer):

```csharp
optionsBuilder.UseOutbox(options =>
{
    options.Serializer = new MyCustomOutboxSerializer();
});
```

> [!IMPORTANT]
> `AddOutbox` maps two tables — `__Outboxes` and `__OutboxMessages`. Include them in your EF Core
> migrations (or ensure the schema exists) before running the delivery service.

### Enqueue a message

Call `AddOutboxMessage` on the context to stage a message, then `SaveChangesAsync` to commit it together
with your other changes. The message payload is serialized, the current `Activity.Current?.Id` is captured
for trace correlation, and the row is written as part of the same transaction:

```csharp
dbContext.AddOutboxMessage(new UserRegistered { UserId = user.Id });
dbContext.SimpleUsers.Add(user);

await dbContext.SaveChangesAsync(cancellationToken);
```

Pass an optional `OutboxMessageDeliveryOptions` to schedule or expire a message:

```csharp
dbContext.AddOutboxMessage(
    new UserRegistered { UserId = user.Id },
    new OutboxMessageDeliveryOptions
    {
        NotBefore = DateTimeOffset.UtcNow.AddMinutes(5), // delay delivery
        NotAfter = DateTimeOffset.UtcNow.AddHours(1)     // discard if not delivered in time
    });
```

| `OutboxMessageDeliveryOptions` member | Type | Effect |
| --- | --- | --- |
| `NotBefore` | `DateTimeOffset?` | Earliest delivery time. `null` delivers as soon as possible. |
| `NotAfter` | `DateTimeOffset?` | Latest delivery time. If not delivered by then, the message is discarded. |

## Deliver messages

Delivery is a hosted background service registered per `DbContext` type. Register it with
`AddOutboxDeliveryService`, choosing a **message handler** (how each message is dispatched) and a database
**provider** (how the outbox is polled and locked).

```csharp
builder.Services
    .AddOutboxDeliveryService<SimpleDbContext, MassTransitOutboxMessageHandler>(options =>
    {
        options.UseSqlServerOutboxProvider();
    });
```

This registers an `OutboxDeliveryService<SimpleDbContext>` hosted service, plus the handler and a fault
handler keyed by `typeof(SimpleDbContext)`. The service polls the outbox, invokes the handler for each
message, and updates delivery bookkeeping — all inside a transaction using the provider's locking strategy.

### Choose a database provider

The provider package supplies a store-optimized `IOutboxProvider`. Call the matching extension inside the
`configureOptions` delegate:

| Provider package | Extension |
| --- | --- |
| `…EntityFrameworkCore.SqlServer` | `options.UseSqlServerOutboxProvider()` |
| `…EntityFrameworkCore.PostgreSql` | `options.UsePostgreSqlOutboxProvider()` |
| `…EntityFrameworkCore.MySql` | `options.UseMySqlOutboxProvider()` |

> [!NOTE]
> If you do not call a provider extension, the delivery service uses a default (non-optimized) provider.
> Prefer the provider that matches your database for correct row-level locking under concurrency.

### Tune the delivery service

`AddOutboxDeliveryService` accepts `Action<OutboxDeliveryOptions<TDbContext>>`. Key knobs:

| `OutboxDeliveryOptions` member | Type | Default | Purpose |
| --- | --- | --- | --- |
| `PollingInterval` | `TimeSpan` | `00:00:10` | Interval between polls. Interrupted early when a new message is enqueued. |
| `BatchSize` | `int` | `10` | Messages retrieved per batch. |
| `DeliveryTimeout` | `TimeSpan?` | `null` | Timeout for acquiring, processing, and releasing a batch. |
| `TransactionCommitTimeout` | `TimeSpan?` | `null` | Timeout for committing after a batch. |
| `TransactionIsolationLevel` | `IsolationLevel` | `RepeatableRead` | Isolation level for acquiring the outbox. |
| `DeliveryServiceEnabled` | `bool` | `true` | Set `false` to register the service but keep it idle. |
| `OutboxProvider` | `IOutboxProvider<TDbContext>` | default provider | Set via `Use…OutboxProvider()`. |

```csharp
builder.Services.AddOutboxDeliveryService<SimpleDbContext, MassTransitOutboxMessageHandler>(options =>
{
    options.UseSqlServerOutboxProvider();
    options.BatchSize = 50;
    options.PollingInterval = TimeSpan.FromSeconds(5);
});
```

## Dispatch with MassTransit

`ES.FX.TransactionalOutbox.MassTransit` provides `MassTransitOutboxMessageHandler`, an `IOutboxMessageHandler`
that publishes each captured message onto the bus via `IPublishEndpoint`. Its `IsReadyAsync` gate is a
non-blocking check that reports whether the bus is `Healthy`; the delivery service polls it on its own
interval, so delivery pauses until MassTransit is up. Register MassTransit as usual, then use the handler
as the delivery handler:

```csharp
builder.Services.AddMassTransit(x =>
{
    x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
});

builder.Services
    .AddOutboxDeliveryService<SimpleDbContext, MassTransitOutboxMessageHandler>(options =>
    {
        options.UseSqlServerOutboxProvider();
    });
```

Published messages carry the payload deserialized to its original CLR type (published as the declared
`MessageType`), and any headers captured with the outbox message are set on the transport message, so your
MassTransit consumers receive strongly-typed messages with their original headers.

## Custom message handlers

To dispatch somewhere other than MassTransit, implement `IOutboxMessageHandler` and pass it as the handler
type argument. The handler receives an `OutboxMessageContext` describing the message and its delivery
bookkeeping:

| `OutboxMessageContext` member | Type | Meaning |
| --- | --- | --- |
| `Message` | `object` | The deserialized message payload. |
| `MessageType` | `Type` | The CLR type of the payload. |
| `Headers` | `IDictionary<string, string>` | Headers captured with the message. |
| `DeliveryAttempts` | `int` | Number of delivery attempts made so far. |
| `DeliveryFirstAttemptedAt` | `DateTimeOffset` | When delivery was first attempted. |
| `DeliveryLastAttemptedAt` | `DateTimeOffset?` | When delivery was last attempted (`null` before the first attempt). |

```csharp
public class LoggingOutboxMessageHandler(ILogger<LoggingOutboxMessageHandler> logger)
    : IOutboxMessageHandler
{
    public ValueTask HandleAsync(OutboxMessageContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Delivering {MessageType} (attempt {Attempt}, first tried {FirstAt})",
            context.MessageType, context.DeliveryAttempts, context.DeliveryFirstAttemptedAt);
        // dispatch context.Message somewhere...
        return ValueTask.CompletedTask;
    }
}
```

```csharp
builder.Services.AddOutboxDeliveryService<SimpleDbContext, LoggingOutboxMessageHandler>(options =>
    options.UseSqlServerOutboxProvider());
```

> [!TIP]
> Override `IOutboxMessageHandler.IsReadyAsync` to delay delivery until a dependency is available (for example,
> a downstream health check). The delivery service does not process messages until `IsReadyAsync` returns `true`.

## Handle delivery faults

When a handler throws, the delivery service consults an `IOutboxMessageFaultHandler`, which returns a
`DeliveryFaultResult` deciding what happens to the message:

- `DeliveryFaultResult.Redeliver(TimeSpan delay)` — retry after `delay`.
- `DeliveryFaultResult.Discard()` — drop the message.

By default `AddOutboxDeliveryService` uses `DefaultOutboxMessageFaultHandler`, which retries with
exponential backoff (10s, doubling, capped at 1 hour). Supply your own by using the three-type-argument
overload:

```csharp
builder.Services
    .AddOutboxDeliveryService<SimpleDbContext, MassTransitOutboxMessageHandler, MyFaultHandler>(options =>
        options.UseSqlServerOutboxProvider());
```

```csharp
public class MyFaultHandler : IOutboxMessageFaultHandler
{
    public ValueTask<DeliveryFaultResult> HandleAsync(
        OutboxMessageFaultContext context, CancellationToken cancellationToken = default)
    {
        // context.FaultException, context.MessageContext.DeliveryAttempts available
        return ValueTask.FromResult(context.MessageContext.DeliveryAttempts > 5
            ? DeliveryFaultResult.Discard()
            : DeliveryFaultResult.Redeliver(TimeSpan.FromMinutes(1)));
    }
}
```

## Observability

The library exposes an OpenTelemetry `ActivitySource` (named after the `ES.FX.TransactionalOutbox`
assembly) that traces outbox acquisition and per-message delivery. Register it on your tracer provider with
`AddOutboxInstrumentation`:

```csharp
builder.Services
    .AddOutboxDeliveryService<SimpleDbContext, MassTransitOutboxMessageHandler>(options =>
        options.UseSqlServerOutboxProvider())
    .AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddOutboxInstrumentation());
```

Because `AddOutboxMessage` captures `Activity.Current?.Id` at enqueue time, delivery spans can be correlated
back to the request or operation that produced the message.

> [!TIP]
> Using [Ignite](../ignite/index.md)? OpenTelemetry is already configured by `builder.Ignite(...)`; you only
> need the `AddOutboxInstrumentation()` call to add the outbox trace source.

## See also

- [Entity Framework Core additions](../additions/entity-framework-core.md) — helpers for the `DbContext` that
  hosts your outbox tables.
- [MassTransit additions](../additions/masstransit.md) — configuring the bus that `MassTransitOutboxMessageHandler`
  publishes onto.
- [Migrations](./migrations.md) — apply the `__Outboxes` / `__OutboxMessages` schema at startup.
- [Ignite overview](../ignite/index.md) — the OpenTelemetry pipeline that `AddOutboxInstrumentation` feeds.
- [Outbox pattern (microservices.io)](https://microservices.io/patterns/data/transactional-outbox.html) — the
  pattern this library implements.
