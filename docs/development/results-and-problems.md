---
title: Results and problems
description: Model success-or-failure without exceptions using ES.FX Result, Result<T>, and the RFC 7807 Problem type.
---

## Overview

`Result` and `Result<T>` let a method return **either a value or a failure** as an ordinary return value,
instead of throwing. Failures are represented by `Problem`, a record modeled on
[RFC 7807 / RFC 9457 (Problem Details for HTTP APIs)](https://datatracker.ietf.org/doc/html/rfc9457), so the
same error shape flows from your domain logic all the way to an HTTP response body.

Reach for this pattern when:

- A failure is an **expected** outcome (validation, "not found", a business-rule rejection) rather than a
  bug — you want the caller to handle it, not catch an exception.
- You want failures to carry structured, serializable detail (`Type`, `Title`, `Detail`, `Status`) instead
  of a bare message string.
- You want the compiler to remind the caller that the operation can fail.

The types live in the framework-agnostic core package `ES.FX`, so they have no ASP.NET, DI, or
third-party dependencies. `Result`/`Result<T>` live in the `ES.FX.Results` namespace; `Problem` and friends
in `ES.FX.Problems`.

> [!NOTE]
> `Result<T>` is a value-or-problem container, not a discriminated union with exhaustiveness checking. It
> is deliberately small: two states, a set of accessors, and implicit conversions that keep call sites
> clean.

## Install

`Result` and `Problem` ship in the core `ES.FX` package.

```bash
dotnet add package ES.FX
```

```xml
<PackageReference Include="ES.FX" />
```

> [!NOTE]
> ES.FX uses Central Package Management. In a consumer that also centralizes versions, omit the `Version`
> attribute (as shown). In a standalone project, add `Version="…"` pinning the release you target.

Then import the namespaces you need:

```csharp
using ES.FX.Results;
using ES.FX.Problems;
```

## Basic usage

A method that can fail returns `Result<T>`. Return a value on success, or a `Problem` on failure — the
implicit conversions mean you rarely name `Result<T>` in the method body.

```csharp
using ES.FX.Results;
using ES.FX.Problems;

public Result<Order> GetOrder(int id)
{
    var order = _repository.Find(id);
    if (order is null)
        return new Problem(
            type: "https://example.com/problems/order-not-found",
            title: "Order not found",
            detail: $"No order exists with id {id}.",
            status: 404);          // implicitly converts Problem -> Result<Order>

    return order;                  // implicitly converts Order -> Result<Order>
}
```

The caller inspects which state came back. The idiomatic way is `TryPickResult` / `TryPickProblem`, which
give you the value or the problem without throwing:

```csharp
var result = GetOrder(42);

if (result.TryPickProblem(out var problem, out var order))
{
    // handle the failure; `order` is null here
    logger.LogWarning("Lookup failed: {Title}", problem.Title);
    return;
}

// `order` is non-null and typed as Order here
Console.WriteLine(order.Total);
```

`Result` (non-generic) is the equivalent for operations that either **succeed with no value** or fail:

```csharp
public Result DeleteOrder(int id)
{
    if (!_repository.Exists(id))
        return new Problem(title: "Order not found", status: 404);

    _repository.Delete(id);
    return Result.Success;
}
```

## API surface

### `Result<T>`

`Result<T>` (where `T : notnull`) holds either a `T` or a `Problem`.

| Member | Signature | Purpose |
| --- | --- | --- |
| Constructor | `Result(T value)` | Create a successful result carrying `value`. |
| Constructor | `Result(Problem problem)` | Create a failed result carrying `problem`. |
| `IsResult` | `bool` | `true` when the result holds a `T`. |
| `IsProblem` | `bool` | `true` when the result holds a `Problem`. |
| `AsResult` | `T` | The value. Throws `InvalidOperationException` if this is a problem. |
| `AsProblem` | `Problem` | The problem. Throws `InvalidOperationException` if this is a value. |
| `Value` | `object` | The underlying value or problem, boxed (from `IResult`). |
| `TryPickResult` | `bool TryPickResult(out T? result)` | `true` + the value on success; `false` otherwise. |
| `TryPickResult` | `bool TryPickResult(out T? result, out Problem? problem)` | `true` + value, or `false` + problem. Prefer this — it gives you both branches. |
| `TryPickProblem` | `bool TryPickProblem(out Problem? problem)` | `true` + the problem when failed. |
| `TryPickProblem` | `bool TryPickProblem(out Problem? problem, out T? result)` | `true` + problem, or `false` + value. |

> [!TIP]
> `IsResult`, `IsProblem`, and the two-out `TryPick*` overloads are annotated with
> `[MemberNotNullWhen]` / `[NotNullWhen]`, so the compiler's nullable flow analysis narrows the correct
> out parameter to non-null in each branch. Prefer them over `AsResult` / `AsProblem`, which throw on the
> wrong state.

### `Result`

`Result : Result<bool>` is the no-value variant. Success carries an implicit `true`.

| Member | Signature | Purpose |
| --- | --- | --- |
| Constructor | `Result()` | Create a successful result. |
| Constructor | `Result(Problem problem)` | Create a failed result. |
| `Result.Success` | `static Result` | A fresh successful result. Use this instead of `new Result()`. |

### `IResult`

Both types implement `IResult`, which exposes a single untyped accessor. It is useful for code that
handles heterogeneous results generically (for example, a middleware that only needs the boxed value).

```csharp
public interface IResult
{
    object Value { get; }
}
```

## Common patterns

### Convert between value/problem and `Result<T>`

The conversions are the reason call sites stay clean. Implicit conversions are **into** `Result<T>`;
extracting the underlying value or problem is **explicit** (a cast), because it can fail.

```csharp
// Implicit: value or problem -> Result<T>
Result<int> ok = 42;
Result<int> bad = new Problem(title: "Nope", status: 400);

// Explicit: Result<T> -> value or problem (throws on the wrong state)
int value = (int)ok;          // == 42
Problem problem = (Problem)bad;
```

> [!WARNING]
> The explicit cast `(T)result` / `(Problem)result` throws `InvalidOperationException` if the result is in
> the other state — it is exactly `AsResult` / `AsProblem`. Guard with `IsResult` / `TryPickResult`
> first, or only cast when you already know the state.

### Chain calls that each return a `Result<T>`

Short-circuit on the first problem by returning it straight through — no re-wrapping needed:

```csharp
public Result<Invoice> CreateInvoice(int orderId)
{
    var orderResult = GetOrder(orderId);
    if (orderResult.TryPickProblem(out var problem, out var order))
        return problem;                        // propagate the problem unchanged

    var priced = Price(order);                 // Result<PricedOrder>
    if (priced.TryPickProblem(out problem, out var pricedOrder))
        return problem;

    return _invoices.Create(pricedOrder);      // Result<Invoice>
}
```

### Model validation failures with `ValidationProblem`

`ValidationProblem : Problem` carries a field-keyed error dictionary and pre-sets a validation `Type` and
`Title`. Return it anywhere a `Problem` is expected.

```csharp
public Result<Customer> Register(RegisterRequest request)
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(request.Email))
        errors[nameof(request.Email)] = ["Email is required."];
    if (request.Age < 18)
        errors[nameof(request.Age)] = ["Must be 18 or older."];

    if (errors.Count > 0)
        return new ValidationProblem(errors);  // Type and Title are set for you

    return _customers.Add(request);
}
```

`ValidationProblem` sets:

- `Type` = `"https://tools.ietf.org/html/rfc9110#section-15.5.1"`
- `Title` = `"One or more validation errors occurred."`
- `Errors` = the `IDictionary<string, string[]>` you supply (field name → messages).

> [!NOTE]
> `ValidationProblem` also has a parameterless constructor `new ValidationProblem()` (which initializes an
> empty `Errors` dictionary) and a settable `Errors` property (`IDictionary<string, string[]> { get; set; }`).
> Use these for model-binding or incremental-build scenarios where you populate `Errors` after construction.

### Throw only at a boundary you control

Keep results flowing through your domain, and convert to an exception only at an edge that expects one
(for example, a code path that cannot return a `Result`). `Problem.Throw()` wraps the problem in a
`ProblemException`:

```csharp
using ES.FX.Problems;

var result = GetOrder(id);
if (result.TryPickProblem(out var problem, out var order))
    problem.Throw();   // throws ProblemException; never returns

// order is non-null past this point
Process(order);
```

`ProblemException` exposes the source problem via its `Problem` property, so a catch site (or a global
exception handler) can recover the structured detail:

```csharp
try
{
    DoWork();
}
catch (ProblemException ex)
{
    Problem problem = ex.Problem;   // the original Problem, intact
    // map to an HTTP response, log, etc.
}
```

## The `Problem` type

`Problem` is a serializable `record` whose members mirror the RFC 7807 / RFC 9457 problem-details fields.
All are optional except `Type`, which defaults to `"about:blank"`.

| Property | Type | Default | Meaning |
| --- | --- | --- | --- |
| `Type` | `string` | `"about:blank"` | URI reference identifying the problem *type*. |
| `Title` | `string?` | `null` | Short, human-readable summary of the type (stable across occurrences). |
| `Detail` | `string?` | `null` | Human-readable explanation specific to *this* occurrence. |
| `Instance` | `string?` | `null` | URI reference identifying this specific occurrence. |
| `Status` | `int?` | `null` | Origin status code (e.g. an HTTP status) for this occurrence. |

Because it is a `record`, `Problem` gets value equality and `with`-expression copying for free:

```csharp
var notFound = new Problem(title: "Order not found", status: 404);
var withDetail = notFound with { Detail = "No order exists with id 42." };
```

> [!NOTE]
> `Problem` also has a parameterless constructor `new Problem()` and all five properties are settable
> (`{ get; set; }`). This supports deserialization and model-binding scenarios where the instance is
> built incrementally rather than through the named-args constructor.

> [!TIP]
> Set `Status` when the problem may cross an HTTP boundary. ASP.NET integrations (see the
> `ES.FX.Additions.Microsoft.AspNetCore` package) can map a `Problem` directly onto a
> `ProblemDetails` response, and `Status` becomes the HTTP status code.

## Equality semantics

`Result<T>` overrides `Equals` and the `==` / `!=` operators so results compare by **state and inner
value**:

- Two results are equal when both are successes with equal values, or both are problems with equal
  problems.
- A success result compares equal to a raw `T` with the same value: `Result<int> r = 5; r == 5` is
  `true`.
- A problem result compares equal to an equal `Problem`.

```csharp
Result<int> a = 5;
Result<int> b = 5;
bool same = a == b;      // true  (same state, equal value)
bool toRaw = a == 5;     // true  (success compared to raw value)

Result<int> p = new Problem(title: "x");
bool mixed = a == p;     // false (different state)
```

> [!NOTE]
> Equality against a raw value or `Problem` works because `==` is overloaded against `object`. This keeps
> assertions and comparisons terse, especially in tests.

## See also

- [Development overview](./index.md) — the rest of the `ES.FX` core building blocks.
- [Primitives](./primitives.md) — `Optional<T>` and other related value types in the core package.
- [FluentValidation additions](../additions/fluentvalidation.md) — producing `ValidationProblem`s from validators.
- [ASP.NET Core additions](../additions/microsoft-aspnetcore.md) — mapping `Problem` onto HTTP `ProblemDetails` responses.
- [RFC 9457 — Problem Details for HTTP APIs](https://datatracker.ietf.org/doc/html/rfc9457) — the specification `Problem` is modeled on.
