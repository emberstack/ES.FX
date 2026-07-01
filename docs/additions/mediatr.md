---
title: MediatR additions
description: Reusable batch request and notification contracts for MediatR, plus a convenience meta-package that bundles MediatR.
---

## Overview

ES.FX ships two focused helpers on top of [MediatR](https://github.com/jbogard/MediatR):

- **`ES.FX.Additions.MediatR.Contracts`** augments `MediatR.Contracts` with generic **batch** message contracts — `BatchRequest<T>` and `BatchNotification<T>` — so you can send or publish a collection of items through a single MediatR message without hand-rolling a wrapper record every time.
- **`ES.FX.Additions.MediatR`** is a thin convenience package that references **MediatR** and the contracts package together. Reference it from your handler-side project to pull both in with one dependency.

Everything else — `IMediator`, `ISender`, `IPublisher`, handler registration, pipeline behaviors — comes straight from MediatR itself. These packages add only the batch contracts; see the [MediatR documentation](https://github.com/jbogard/MediatR/wiki) for the base API.

> [!NOTE]
> There is no Ignite Spark for MediatR. These are plain library helpers with no DI, health-check, or observability wiring. Register MediatR the usual way (`services.AddMediatR(...)`) and use these contracts as your message types.

## Install

Install the contracts package if you only need the batch message types (for example, in a shared contracts assembly):

```bash
dotnet add package ES.FX.Additions.MediatR.Contracts
```

```xml
<PackageReference Include="ES.FX.Additions.MediatR.Contracts" />
```

Install the meta-package to pull in **both** MediatR and the contracts in a single reference (for example, in the project that hosts your handlers):

```bash
dotnet add package ES.FX.Additions.MediatR
```

```xml
<PackageReference Include="ES.FX.Additions.MediatR" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so an in-repo `<PackageReference>` carries no `Version` attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

> [!TIP]
> `ES.FX.Additions.MediatR` references `ES.FX.Additions.MediatR.Contracts`, so installing the meta-package also gives you `BatchRequest<T>` and `BatchNotification<T>`. Reference only `ES.FX.Additions.MediatR.Contracts` when you want the message shapes without taking a dependency on MediatR's handler runtime.

## What it adds

Both types live in the `ES.FX.Additions.MediatR.Contracts.Batches` namespace.

| Type | MediatR interface | Purpose |
| --- | --- | --- |
| `BatchRequest<T>` | `IRequest` | A request carrying a batch of `T` items, dispatched to a single handler via `ISender`/`IMediator`. |
| `BatchNotification<T>` | `INotification` | A notification carrying a batch of `T` items, broadcast to all handlers via `IPublisher`/`IMediator`. |

Both are `record` types with a single init-only collection property:

```csharp
public record BatchRequest<T> : IRequest
{
    public IReadOnlyList<T> Items { get; init; } = [];
}

public record BatchNotification<T> : INotification
{
    public IReadOnlyList<T> Items { get; init; } = [];
}
```

`Items` defaults to an empty list, so a message with no items is valid and never null.

## Usage

### Handle a batch request

`BatchRequest<T>` implements `IRequest` (the void-returning variant), so it maps to an `IRequestHandler<BatchRequest<T>>`.

```csharp
using ES.FX.Additions.MediatR.Contracts.Batches;
using MediatR;

// Send a batch of orders to be processed.
await sender.Send(new BatchRequest<Order>
{
    Items = ordersToProcess
});

// The single handler for the batch.
public sealed class ProcessOrdersHandler : IRequestHandler<BatchRequest<Order>>
{
    public async Task Handle(BatchRequest<Order> request, CancellationToken cancellationToken)
    {
        foreach (var order in request.Items)
        {
            // process each order…
        }
    }
}
```

### Publish a batch notification

`BatchNotification<T>` implements `INotification`, so every registered `INotificationHandler<BatchNotification<T>>` receives it.

```csharp
using ES.FX.Additions.MediatR.Contracts.Batches;
using MediatR;

// Broadcast that a batch of orders shipped.
await publisher.Publish(new BatchNotification<Order>
{
    Items = shippedOrders
});

// One of possibly many handlers.
public sealed class NotifyWarehouseHandler : INotificationHandler<BatchNotification<Order>>
{
    public Task Handle(BatchNotification<Order> notification, CancellationToken cancellationToken)
    {
        // react to the batch…
        return Task.CompletedTask;
    }
}
```

### Register MediatR

These contracts are just message types — register MediatR itself as usual so it can discover the handlers:

```csharp
using ES.FX.Additions.MediatR.Contracts.Batches;
using MediatR;

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<ProcessOrdersHandler>());
```

## Notes and limitations

- **Contracts only.** These packages add message shapes, not behavior. There is no batch-splitting, fan-out, or partial-failure handling — a batch handler receives the whole `Items` collection and decides what to do with it.
- **`BatchRequest<T>` returns no value.** It implements `IRequest` (void), not `IRequest<TResponse>`. If you need a response per batch, define your own request implementing `IRequest<TResponse>`.
- **`Items` is init-only and typed `IReadOnlyList<T>`.** Assign the full batch at construction time; the list interface is read-only, but element instances are only as immutable as `T` itself.
- **No Ignite integration.** MediatR has no Spark. Wire it through MediatR's own DI extensions.

## See also

- [MediatR](https://github.com/jbogard/MediatR/wiki) — the upstream mediator library and its handler/pipeline API.
- [MediatR.Contracts](https://www.nuget.org/packages/MediatR.Contracts) — the base `IRequest` / `INotification` contracts these types build on.
- [Additions](./index.md) — the full catalog of ES.FX Additions.
- [MassTransit additions](./masstransit.md) — messaging helpers when you move from in-process mediation to a message bus.
