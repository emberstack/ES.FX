---
title: MassTransit additions
description: Message-kind headers, kind-aware name formatters, and MediatR bridge consumers that augment MassTransit.
---

MassTransit is a distributed application framework for .NET that abstracts message transports such as
RabbitMQ, Azure Service Bus, and Amazon SQS. The ES.FX MassTransit additions are small, low-opinion
helpers layered on top of it, split across three packages:

- **`ES.FX.Additions.MassTransit`** — a portable **message-kind** mechanism (a stable, type-independent
  identifier carried in a transport header) plus kind-aware endpoint and entity name formatters.
- **`ES.FX.Additions.MassTransit.MediatR`** — MassTransit consumers that forward consumed messages to
  [MediatR](https://github.com/jbogard/MediatR), so a single handler can serve both in-process and
  transport-delivered messages.
- **`ES.FX.Additions.MassTransit.RabbitMQ`** — a minimal `RabbitMqOptions` binding type for RabbitMQ
  connection configuration.

These packages add helpers only; they do not wire MassTransit into Ignite. You configure MassTransit
itself with its own `AddMassTransit(...)` builder — see the
[MassTransit documentation](https://masstransit.io/documentation/concepts) for the base API.

> [!NOTE]
> There is no `MassTransit` Spark. Register and configure the bus with MassTransit's own
> `IServiceCollection.AddMassTransit(...)`, then apply these helpers inside your bus configuration.

## Overview

The core problem these additions solve is **message identity across services**. By default MassTransit
routes and (de)serializes messages using the .NET `Type` name and its assembly-qualified `MessageUrn`.
That couples the wire format to your CLR types: rename or move a message class and existing messages no
longer match.

The `MassTransit` addition decouples the two by introducing a **kind** — a short, stable string you
attach to a message type with the `[Kind]` attribute (`ES.FX.ComponentModel.DataAnnotations.KindAttribute`
from the core `ES.FX` package). The kind is written to an `X-ES-FX-Kind` transport header on publish and
can be resolved back to the correct .NET type on receive. Name formatters use the same kind to produce
predictable exchange/queue names that don't leak namespaces.

The `MassTransit.MediatR` addition bridges the two mediators: it lets a MassTransit consumer hand the
message straight to MediatR (`IMediator.Publish` for notifications, `IMediator.Send` for requests),
including batched delivery.

## Install

`ES.FX.Additions.MassTransit`:

```bash
dotnet add package ES.FX.Additions.MassTransit
```

```xml
<PackageReference Include="ES.FX.Additions.MassTransit" />
```

`ES.FX.Additions.MassTransit.MediatR` (references `ES.FX.Additions.MassTransit`,
`ES.FX.Additions.MediatR.Contracts`, `MassTransit.Abstractions`, and `MediatR`):

```bash
dotnet add package ES.FX.Additions.MassTransit.MediatR
```

```xml
<PackageReference Include="ES.FX.Additions.MassTransit.MediatR" />
```

`ES.FX.Additions.MassTransit.RabbitMQ` (references `MassTransit.RabbitMQ`):

```bash
dotnet add package ES.FX.Additions.MassTransit.RabbitMQ
```

```xml
<PackageReference Include="ES.FX.Additions.MassTransit.RabbitMQ" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so `<PackageReference>` in a repo that also centralizes versions
> carries no `Version` attribute. In a standalone consumer, add `Version="…"`.

## What it adds

### `ES.FX.Additions.MassTransit` — message kind

| Member | Signature | Purpose |
| --- | --- | --- |
| `MessageKindExtensions.UseMessageKind` | `void UseMessageKind(this IBusFactoryConfigurator cfg, IRegistrationContext registrationContext)` | Wires the message-kind pipeline into a bus: observes configured consumers to register their message types, and adds a publish filter that stamps the `X-ES-FX-Kind` header. |
| `MessageKindProvider.Header` | `const string` (`"X-ES-FX-Kind"`) | The transport header name the kind is written to and read from. |
| `MessageKindProvider.RegisterTypes` | `void RegisterTypes(params Type[] types)` | Registers message types so their kind (from `[Kind]`) can be resolved back to a `Type`. Re-registering the same type is idempotent; registering a kind already mapped to a *different* type throws `InvalidOperationException`. |
| `MessageKindProvider.GetType` | `Type? GetType(string? kind)` / `Type? GetType(ReceiveContext receiveContext)` | Resolves a kind string (or the kind header on a received message) to the registered .NET type, or `null`. |
| `MessageKindPublishFilter<T>` | `IFilter<PublishContext<T>>` | Publish filter that sets the kind header from the runtime message type's `[Kind]`, falling back to `T`'s `[Kind]` (so interface message contracts get the header). No header is set when no kind is defined. Installed by `UseMessageKind`; rarely used directly. |
| `TryResendUsingMessageKindFilter` | `IFilter<ReceiveContext>` | Filter for the **dead-letter (skipped) pipe** that looks up the type by kind header and re-dispatches the message deserialized as that type, preserving `RequestId`, `ResponseAddress`, and `FaultAddress`. |

### `ES.FX.Additions.MassTransit` — name formatters

| Type | Constructor | Purpose |
| --- | --- | --- |
| `KindEndpointNameFormatter` | `KindEndpointNameFormatter(string joinSeparator = "", string prefix = "", bool includeNamespace = false)` | An `IEndpointNameFormatter` (extends `DefaultEndpointNameFormatter`) that names endpoints from `[Kind]` when present, falling back to the default formatting otherwise. |
| `KindEntityNameFormatter` | `KindEntityNameFormatter(IEntityNameFormatter entityNameFormatter, bool faultFallbackToKind = true, string faultFormat = "{0}_fault")` | An `IEntityNameFormatter` that names entities from `[Kind]` / `[FaultKind]`. For `Fault<T>` it uses `[FaultKind]`, otherwise (when `faultFallbackToKind` is `true`) formats the message's kind with `faultFormat`. |
| `AggregatePrefixEntityNameFormatter` | `AggregatePrefixEntityNameFormatter(IEntityNameFormatter entityNameFormatter, string? separator = null, params Func<Type, string?>[] prefixProviders)` | An `IEntityNameFormatter` that prepends one or more computed prefixes (e.g. environment or tenant) to a base formatter's name, joined by `separator`. |

> [!NOTE]
> The `[Kind]` and `[FaultKind]` attributes (`KindAttribute` / `FaultKindAttribute`) live in the core
> `ES.FX` package under `ES.FX.ComponentModel.DataAnnotations`. Apply them to message classes or
> interfaces. `KindAttribute.For(type)` returns the kind string, or `null` when the attribute is absent.

### `ES.FX.Additions.MassTransit.MediatR` — bridge consumers

| Type | Shape | Purpose |
| --- | --- | --- |
| `MediatorConsumer<TMessage>` | `IConsumer<TMessage>` (ctor takes `IMediator`) | Consumes `TMessage` and forwards it to MediatR, deciding from the **runtime message instance**: `IMediator.Publish` when the message is an `INotification`, `IMediator.Send` when it is an `IRequest`. Throws `InvalidOperationException` for any other message type. |
| `MediatorBatchConsumer<TMessage>` | `IConsumer<Batch<TMessage>>` (ctor takes `IMediator`) | Consumes a MassTransit `Batch<TMessage>` and forwards it as a single `BatchNotification<TMessage>` (for `INotification`) or `BatchRequest<TMessage>` (for `IRequest`) from `ES.FX.Additions.MediatR.Contracts`, deciding from the **static `typeof(TMessage)`** generic parameter. No-ops on an empty batch. |

### `ES.FX.Additions.MassTransit.RabbitMQ`

| Type | Members | Purpose |
| --- | --- | --- |
| `RabbitMqOptions` | `string Host`, `string? Username`, `string? Password` | A plain options type for binding RabbitMQ connection settings from configuration, to pass into MassTransit's RabbitMQ host configuration. |

## Usage

### Attach a kind and enable the kind pipeline

Mark message types with `[Kind]`, then call `UseMessageKind` inside the bus factory configuration.
`UseMessageKind` needs the `IRegistrationContext` (available in the `AddMassTransit` bus callback) so the
publish filter can be added.

```csharp
using ES.FX.Additions.MassTransit.MessageKind;
using ES.FX.ComponentModel.DataAnnotations;
using MassTransit;

[Kind("order-submitted")]
public record OrderSubmitted(Guid OrderId);

builder.Services.AddMassTransit(mt =>
{
    mt.AddConsumer<OrderSubmittedConsumer>();

    mt.UsingInMemory((context, cfg) =>
    {
        // Observes configured consumers to register their message types and
        // stamps the X-ES-FX-Kind header on every published message.
        cfg.UseMessageKind(context);

        cfg.ConfigureEndpoints(context);
    });
});
```

On publish, `OrderSubmitted` now carries the header `X-ES-FX-Kind: order-submitted`. On the receiving
side, `MessageKindProvider.GetType(receiveContext)` resolves that header back to the `OrderSubmitted`
type — provided the type was registered (consumers registered via `UseMessageKind` register
automatically; otherwise call `MessageKindProvider.RegisterTypes(typeof(OrderSubmitted))`).

### Recover a message when its declared type can't be resolved

Attach `TryResendUsingMessageKindFilter` to the **dead-letter (skipped) pipe** to fall back to the kind
header when the incoming message's declared type isn't recognized (for example, a producer used a
different CLR type name). If the kind resolves to a registered type, the message is deserialized as that
type and re-sent to the endpoint (envelope metadata such as `RequestId`, `ResponseAddress`, and
`FaultAddress` is preserved); otherwise the pipeline continues unchanged.

```csharp
using ES.FX.Additions.MassTransit.MessageKind;

mt.AddConfigureEndpointsCallback((_, endpoint) =>
{
    // Attempt to fix the message type and resend; otherwise dead-letter as usual.
    endpoint.ConfigureDeadLetter(pipe =>
        pipe.UseFilter(new TryResendUsingMessageKindFilter()));
});
```

> [!WARNING]
> Do not attach this filter to a regular receive pipe. The kind header is stamped on every published
> message, so every kind-annotated message would be resent instead of consumed.

### Use kind-based names

Replace the default endpoint/entity name formatters so queue and exchange names come from `[Kind]`
instead of type names. `AggregatePrefixEntityNameFormatter` layers computed prefixes (such as an
environment tag) on top.

```csharp
using ES.FX.Additions.MassTransit.Formatters;
using ES.FX.ComponentModel.DataAnnotations;
using MassTransit;

mt.SetEndpointNameFormatter(new KindEndpointNameFormatter(prefix: "prod-"));

mt.UsingInMemory((context, cfg) =>
{
    // Kind-based entity names, then prefixed with the environment.
    cfg.MessageTopology.SetEntityNameFormatter(
        new AggregatePrefixEntityNameFormatter(
            new KindEntityNameFormatter(cfg.MessageTopology.EntityNameFormatter),
            separator: ".",
            _ => "prod"));

    cfg.ConfigureEndpoints(context);
});
```

### Forward MassTransit messages to MediatR

Register the bridge consumer so a message delivered over the transport is handled by your existing
MediatR handler. The message type must implement `INotification` or `IRequest`.

```csharp
using ES.FX.Additions.MassTransit.MediatR.Consumers;
using MassTransit;
using MediatR;

public record OrderSubmitted(Guid OrderId) : INotification;

builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<Program>());

builder.Services.AddMassTransit(mt =>
{
    // MediatorConsumer<OrderSubmitted> forwards to IMediator.Publish(...)
    mt.AddConsumer<MediatorConsumer<OrderSubmitted>>();

    mt.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
});
```

For batched delivery, register `MediatorBatchConsumer<OrderSubmitted>` (configured with MassTransit's
batch options); it forwards the batch as a single `BatchNotification<OrderSubmitted>` or
`BatchRequest<OrderSubmitted>` from `ES.FX.Additions.MediatR.Contracts`, so add a MediatR handler for
that batch type.

### Bind RabbitMQ connection settings

```json
{
  "RabbitMq": {
    "Host": "rabbitmq://localhost",
    "Username": "guest",
    "Password": "guest"
  }
}
```

```csharp
using ES.FX.Additions.MassTransit.RabbitMQ.Configuration;
using MassTransit;

var rabbit = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>()!;

builder.Services.AddMassTransit(mt =>
{
    mt.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbit.Host, h =>
        {
            h.Username(rabbit.Username ?? "guest");
            h.Password(rabbit.Password ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

## Notes and limitations

- These packages augment MassTransit only — they do **not** register or start the bus, add health
  checks, or wire OpenTelemetry into Ignite. Configure MassTransit with its own `AddMassTransit(...)`.
- The kind mechanism relies on the `[Kind]` attribute. A message type without `[Kind]` produces no kind
  header, and `MessageKindProvider.GetType` returns `null` for an unknown or unregistered kind.
- `MessageKindProvider` keeps a process-wide type cache. Types are registered automatically for consumers
  configured after `UseMessageKind`; register any other publishable types yourself with `RegisterTypes`.
- `MediatorConsumer<T>` / `MediatorBatchConsumer<T>` throw `InvalidOperationException` for message types
  that are neither `INotification` nor `IRequest`.
- **Dispatch asymmetry (by design).** `MediatorConsumer<T>` decides `Publish` vs `Send` by
  pattern-matching the **runtime message instance** (`context.Message`), while `MediatorBatchConsumer<T>`
  gates on the **static `typeof(T)`** generic parameter (`Type.IsAssignableTo`). A single message always
  has a concrete instance to inspect; a batch has no single instance, so it is classified from the element
  type. For a `T` that implements both `INotification` and `IRequest`, both consumers prefer `Publish`
  (notification), so their observable routing stays consistent despite the different mechanism.
- `RabbitMqOptions` is a plain binding type. It does not connect to RabbitMQ or configure the host on its
  own — you pass its values into MassTransit's RabbitMQ configuration as shown above.

## See also

- [MassTransit documentation](https://masstransit.io/documentation/concepts) — the base bus, consumers,
  and topology APIs these helpers extend.
- [MediatR](https://github.com/jbogard/MediatR) — the in-process mediator the bridge consumers forward to.
- [MediatR additions](./mediatr.md) — the MediatR helpers and the `Batch*` contracts used by
  `MediatorBatchConsumer<T>`.
- [Transactional Outbox](../libraries/transactional-outbox.md) — pairs with
  `ES.FX.TransactionalOutbox.MassTransit` to dispatch captured messages onto the bus.
