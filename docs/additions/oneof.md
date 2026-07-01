---
title: OneOf additions
description: Reusable discriminated-union case types and a Problem accessor that augment the OneOf library.
---

## Overview

[OneOf](https://github.com/mcintyre321/OneOf) gives you F#-style discriminated unions in C# (`OneOf<T0, T1, …>`),
but the case types you slot into a union are yours to define. `ES.FX.Additions.OneOf` fills that gap with a
small vocabulary of ready-made **case types** for the outcomes that keep recurring across services —
`Failure`, `Fault`, `TimedOut`, `Canceled`, `InProgress`, and friends — plus a bridge that lets a union carry
an ES.FX [`Problem`](../development/results-and-problems.md) and be unwrapped in one call.

This package augments exactly one dependency, `OneOf`. It does **not** replace it: you still build unions with
`OneOf<…>` and match them with the upstream `Match`/`Switch`/`TryPickT*` API. Everything here is a type or an
extension you compose into that API.

## Install

```bash
dotnet add package ES.FX.Additions.OneOf
```

```xml
<PackageReference Include="ES.FX.Additions.OneOf" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so a `<PackageReference>` in a project that also centralizes versions
> carries no `Version` attribute. If you consume the package from a project without CPM, add `Version="…"`.

The `OneOf` package is a transitive dependency, so you get it automatically. Use its documentation for the base
union API.

## What it adds

### Case types (`ES.FX.Additions.OneOf.Types`)

Each concept ships as a pair of `record struct` types: a parameterless marker for "this state, no payload", and
a generic `<T>` variant that carries a value. Because they are records, you get value equality, deconstruction,
and `with` expressions for free.

| Type | Signature | Typical meaning |
| --- | --- | --- |
| `Failure` / `Failure<T>` | `record struct Failure;` / `record struct Failure<T>(T Value)` | An expected, recoverable failure. |
| `Fault` / `Fault<T>` | `record struct Fault;` / `record struct Fault<T>(T Value)` | An error condition, typically carrying detail. |
| `Fatal` / `Fatal<T>` | `record struct Fatal;` / `record struct Fatal<T>(T Value)` | An unrecoverable error. |
| `TimedOut` / `TimedOut<T>` | `record struct TimedOut;` / `record struct TimedOut<T>(T Value)` | The operation exceeded its time budget. |
| `Canceled` / `Canceled<T>` | `record struct Canceled;` / `record struct Canceled<T>(T Value)` | The operation was canceled. |
| `Interrupted` / `Interrupted<T>` | `record struct Interrupted;` / `record struct Interrupted<T>(T Value)` | The operation was interrupted before completing. |
| `InProgress` / `InProgress<T>` | `record struct InProgress;` / `record struct InProgress<T>(T Value)` | The operation is still running. |
| `Deferred` / `Deferred<T>` | `record struct Deferred;` / `record struct Deferred<T>(T Value)` | The operation was accepted but deferred. |
| `Unknown` / `Unknown<T>` | `record struct Unknown;` / `record struct Unknown<T>(T Value)` | State could not be determined. |

The generic variant's positional parameter is named `Value`, so you read the payload as `.Value` (for
example `timeout.Value`).

### `Problem` bridge (`ES.FX.Additions.OneOf.Problems`)

| Member | Signature | Purpose |
| --- | --- | --- |
| `IOneOfWithProblem` | `interface IOneOfWithProblem : IOneOf` | Marker interface for a union whose cases include a `Problem`. |
| `OneOfProblemExtensions.TryPickProblem` | `bool TryPickProblem(this IOneOfWithProblem oneOf, out Problem? problem)` | Returns `true` and sets `problem` when the union's current value is a [`Problem`](../development/results-and-problems.md); otherwise `false`. |

`TryPickProblem` inspects the boxed `IOneOf.Value` at runtime, so it works for any union case index that holds a
`Problem` without you knowing which `TryPickT*` slot it lives in. The `out` parameter is annotated
`[NotNullWhen(true)]`, so the compiler treats `problem` as non-null inside the success branch.

## Usage

### Model an outcome with the case types

Compose the case types into a `OneOf<…>` and match them with the upstream API:

```csharp
using ES.FX.Additions.OneOf.Types;
using OneOf;

OneOf<string, TimedOut, Canceled> FetchName(CancellationToken cancellationToken)
{
    if (cancellationToken.IsCancellationRequested) return new Canceled();
    // … return "Ada" on success, or new TimedOut() when the deadline passes …
    return "Ada";
}

var result = FetchName(cancellationToken);

var message = result.Match(
    name => $"Got {name}",
    _ /* TimedOut */ => "Timed out",
    _ /* Canceled */ => "Canceled");
```

Use the generic variant when the case needs to carry data:

```csharp
using ES.FX.Additions.OneOf.Types;
using OneOf;

OneOf<Guid, Failure<string>> CreateOrder(OrderRequest request)
{
    if (!request.IsValid) return new Failure<string>("Invalid order");
    return Guid.NewGuid();
}

if (CreateOrder(request).TryPickT1(out var failure, out var orderId))
{
    Console.WriteLine(failure.Value); // "Invalid order"
}
```

### Carry and unwrap a `Problem`

Implement `IOneOfWithProblem` on a union that can produce a [`Problem`](../development/results-and-problems.md),
then unwrap it with `TryPickProblem` regardless of which slot the problem occupies:

```csharp
using ES.FX.Additions.OneOf.Problems;
using ES.FX.Problems;
using OneOf;

[GenerateOneOf]
public partial class UserResult : OneOfBase<User, Problem>, IOneOfWithProblem;

public UserResult GetUser(int id) =>
    id <= 0
        ? new Problem { Title = "Invalid id" }
        : new User(id);

var outcome = GetUser(-1);

if (outcome.TryPickProblem(out var problem))
{
    // problem is non-null here
    logger.LogWarning("Request failed: {Title}", problem.Title);
}
```

> [!TIP]
> `[GenerateOneOf]` and `OneOfBase<…>` come from OneOf and its source generator (both referenced by this
> package). `TryPickProblem` is the value this Addition contributes on top — a single, slot-agnostic way to pull
> the `Problem` out.

## Notes and limitations

- **These are plain types, not behavior.** The case types have no methods, conversions, or validation — they
  are lightweight tags/payload holders you compose into `OneOf<…>`. Matching, mapping, and slot access all come
  from the upstream OneOf API.
- **No DI or Ignite wiring.** This is a pure helper library with no `Ignite{Service}` extension and no
  registration. Reference it directly wherever you model outcomes.
- **`TryPickProblem` reads the boxed value.** It matches against the runtime type of `IOneOf.Value`, so the
  union must actually be holding a `Problem` for it to return `true`; it does not convert other cases into a
  `Problem`. Passing a `null` union throws `ArgumentNullException`.
- **`Problem` lives in the core `ES.FX` package**, which this Addition depends on. See
  [Results & Problems](../development/results-and-problems.md) for how to build and enrich a `Problem`.

## See also

- [OneOf on GitHub](https://github.com/mcintyre321/OneOf) — the upstream discriminated-union library and its
  `Match`/`Switch`/`TryPickT*` API.
- [Results & Problems](../development/results-and-problems.md) — the `Problem` type carried by
  `IOneOfWithProblem` and `TryPickProblem`.
- [Additions overview](./index.md) — the full catalog of focused, single-dependency helper packages.
