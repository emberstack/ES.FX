---
title: Primitives
description: Optional, DurationValue/DurationUnit, and ValueRange value types from the core ES.FX package.
---

The core `ES.FX` package ships a small set of framework-agnostic value types in the `ES.FX.Primitives`
namespace. They are lightweight, allocation-friendly `struct`s that fill gaps in the BCL for expressing
optional values, unit-tagged durations, and closed ranges — without pulling in any third-party
dependency or requiring Ignite.

## Overview

Three primitives live under `ES.FX.Primitives`:

| Type | Namespace | Solves |
| --- | --- | --- |
| `Optional<T>` | `ES.FX.Primitives` | An explicit "value may or may not be present" wrapper that distinguishes *absent* from `null` and serializes cleanly. |
| `DurationValue` + `DurationUnit` | `ES.FX.Primitives` | A duration expressed as a count plus a unit (`7 Days`, `30 Minutes`), keeping the unit intent instead of collapsing to a bare `TimeSpan`. |
| `ValueRange<T>` | `ES.FX.Primitives` | A closed `[Min, Max]` range over any `IComparable<T>`, with containment and intersection. |

All three are immutable `readonly struct`s. `DurationValue` and `ValueRange<T>` are `record struct`s
(value equality out of the box); `Optional<T>` is a plain `readonly struct`.

> [!NOTE]
> These are core primitives — they have no dependency on Ignite, ASP.NET Core, or any Addition. Reference
> `ES.FX` and use them anywhere.

## Install

The primitives ship in the base `ES.FX` package.

```bash
dotnet add package ES.FX
```

```xml
<PackageReference Include="ES.FX" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so the in-repo `<PackageReference>` carries no `Version`
> attribute. If you consume the package from a project that does not centralize versions, add
> `Version="…"`.

## `Optional<T>`

`Optional<T>` models a value that may be present or absent. Unlike a nullable reference, it lets you
represent an *absent* value that is distinct from a *present but `null`* value — useful for patch
semantics ("this field was omitted" vs. "this field was explicitly set to null").

### Basic usage

Create optionals with the static factories, then inspect `HasValue`:

```csharp
using ES.FX.Primitives;

Optional<string> some = Optional<string>.From("hello");
Optional<string> none = Optional<string>.None();

if (some.HasValue)
{
    Console.WriteLine(some.Value); // "hello"
}

// Safe reads that never throw:
string a = none.GetValueOrDefault("fallback"); // "fallback"
string? b = none.GetValueOrDefault();          // null (default for string)
```

> [!WARNING]
> Reading `.Value` when `HasValue` is `false` throws `InvalidOperationException`. Use
> `GetValueOrDefault(...)`, `TryGetValue(...)`, or `Match(...)` when absence is possible. Note also that
> `.Value` is nullable — a *present* value can still be `null`.

### API surface

| Member | Signature | Purpose |
| --- | --- | --- |
| ctor | `Optional(T? value, bool hasValue)` | Construct directly from a value and a presence flag. Also the `[JsonConstructor]`; prefer `From`/`None` for clarity. |
| `From` | `static Optional<T> From(T value)` | Create a present optional wrapping `value`. |
| `None` | `static Optional<T> None()` | Create an absent optional. |
| `HasValue` | `bool HasValue { get; }` | Whether a value is present. |
| `Value` | `T? Value { get; }` | The value; throws `InvalidOperationException` when absent. May be `null`. |
| `GetValueOrDefault` | `T? GetValueOrDefault()` | The value if present, otherwise `default(T)`. Never throws. |
| `GetValueOrDefault` | `T GetValueOrDefault(T defaultValue)` | The value if present, otherwise `defaultValue`. |
| `TryGetValue` | `bool TryGetValue(out T? value)` | `true` and the value when present; `false` and `default` otherwise. |
| `Match` | `TResult Match<TResult>(Func<T?, TResult> whenSome, Func<TResult> whenNone)` | Branch on presence and return a result. |

### Match on presence

`Match` is the branch-free way to fold both cases into a single result:

```csharp
Optional<int> age = Optional<int>.From(42);

string label = age.Match(
    whenSome: value => $"age is {value}",
    whenNone: () => "age unknown");
```

The `TryGetValue` pattern is idiomatic when you want a local:

```csharp
if (option.TryGetValue(out var value))
{
    // use value
}
```

> [!NOTE]
> `Optional<T>` carries a `[JsonConstructor]`, so `System.Text.Json` round-trips it as
> `{ "value": …, "hasValue": true|false }` without extra configuration.

## `DurationValue` and `DurationUnit`

`DurationValue` is a quantity of time paired with an explicit `DurationUnit` — for example `new
DurationValue(7, DurationUnit.Day)`. It preserves the *unit intent* (7 days, not 604 800 seconds), which
is handy for configuration, display, and calendar-aware units that a `TimeSpan` cannot represent.

### Basic usage

```csharp
using ES.FX.Primitives;

var retention = new DurationValue(7, DurationUnit.Day);

Console.WriteLine(retention.Value); // 7
Console.WriteLine(retention.Unit);  // Day
Console.WriteLine(retention);       // "7 Days"

var single = new DurationValue(1, DurationUnit.Hour);
Console.WriteLine(single);          // "1 Hour"  (singular when Value == 1)
```

> [!WARNING]
> The constructor rejects negative values — `new DurationValue(-1, DurationUnit.Second)` throws
> `ArgumentOutOfRangeException`.

### API surface

| Member | Signature | Purpose |
| --- | --- | --- |
| ctor | `DurationValue(long value, DurationUnit unit)` | Create a non-negative duration; throws `ArgumentOutOfRangeException` if `value < 0`. |
| `Value` | `long Value { get; init; }` | The number of units. |
| `Unit` | `DurationUnit Unit { get; init; }` | The unit of time. |
| `CompareTo` | `int CompareTo(DurationValue other)` | Orders by `Value`; throws `InvalidOperationException` if the units differ. |
| `ToString` | `override string ToString()` | Human-readable form such as `"7 Days"` (pluralized unless `Value == 1`). |
| operators | `>`, `<`, `>=`, `<=` | Comparison operators, same-unit only. |

`DurationValue` implements `IComparable<DurationValue>` and, being a `record struct`, gets value equality
for free.

> [!IMPORTANT]
> Comparison is **same-unit only**. Comparing `5 Days` to `3 Hours` (via `CompareTo` or any of the
> `<`/`>` operators) throws `InvalidOperationException`. `DurationValue` deliberately does not convert
> between units — normalize to a common unit yourself before comparing.

### The `DurationUnit` scale

`DurationUnit` spans fixed-duration units (`Tick` through `Hour`) and calendar-duration units (`Day`
through `Millennium`):

`Tick`, `Nanosecond`, `Microsecond`, `Millisecond`, `Second`, `Minute`, `Hour`, `Day`, `Weekend`,
`Week`, `Month`, `Quarter`, `Year`, `Decade`, `Century`, `Millennium`.

Fixed units map to `TimeSpan` properties; calendar units (`Month`, `Year`, …) require `DateTime`
arithmetic because their length varies. `DurationValue` stores the unit but does not itself convert to a
`TimeSpan` — you decide how to interpret each unit for your domain.

## `ValueRange<T>`

`ValueRange<T>` is an immutable, inclusive `[Min, Max]` range over any `T` that implements
`IComparable<T>`. It offers containment tests and intersection, so you can express and combine bounds
without hand-rolling comparisons.

### Basic usage

```csharp
using ES.FX.Primitives;

var range = new ValueRange<int>(1, 10);

bool inside = range.Contains(5);   // true
bool outside = range.Contains(11); // false
bool exact = range.IsExact();      // false (Min != Max)

Console.WriteLine(range);          // "[1, 10]"

var point = new ValueRange<int>(7); // Min == Max == 7
Console.WriteLine(point.IsExact()); // true
```

> [!WARNING]
> The two-argument constructor requires `min <= max`. `new ValueRange<int>(10, 1)` throws
> `ArgumentException`.

### API surface

| Member | Signature | Purpose |
| --- | --- | --- |
| ctor | `ValueRange(T exact)` | A degenerate range where `Min == Max == exact`. |
| ctor | `ValueRange(T min, T max)` | A range; throws `ArgumentException` if `min > max`. |
| ctor | `ValueRange(ValueRange<T> range)` | Copy constructor. |
| `Min` | `T Min { get; init; }` | The inclusive lower bound. |
| `Max` | `T Max { get; init; }` | The inclusive upper bound. |
| `Contains` | `bool Contains(T value)` | `true` if `Min <= value <= Max`. |
| `Intersect` | `ValueRange<T>? Intersect(ValueRange<T> other)` | The overlap of two ranges, or `null` when they do not overlap. |
| `IsExact` | `bool IsExact()` | `true` when `Min == Max`. |
| `CompareTo` | `int CompareTo(ValueRange<T>? other)` | Orders by `Min`, then `Max`; a `null` other sorts first. |
| `ToString` | `override string ToString()` | Renders as `"[Min, Max]"`. |

### Intersect two ranges

`Intersect` returns the overlapping range, or `null` when the two ranges are disjoint:

```csharp
var a = new ValueRange<int>(1, 10);
var b = new ValueRange<int>(5, 20);

ValueRange<int>? overlap = a.Intersect(b); // [5, 10]

var disjoint = a.Intersect(new ValueRange<int>(50, 60)); // null
```

Because `ValueRange<T>` is a `record struct`, two ranges with the same `Min` and `Max` are equal by
value.

### Binding from configuration

`Min` and `Max` are `init` accessors on purpose: `IConfiguration` binding and most serializers construct a
value type through its parameterless `struct` constructor and then **assign** the members, so a range binds
straight out of config.

```jsonc
// appsettings.json
{ "Retries": { "Min": 3, "Max": 7 } }
```

```csharp
var retries = configuration.GetSection("Retries").Get<ValueRange<int>>(); // [3, 7]
```

> [!WARNING]
> The `min <= max` check lives in the value constructors and is **not** re-run for object initializers,
> `with` expressions, or bound configuration. Making the bounds get-only would enforce the invariant on
> those paths but silently breaks binding (you get a `default` `[0, 0]` range) — a `struct` can always be
> produced as `default` and skip every constructor anyway, so validate untrusted config at the edge if you
> need it.

## Related string helpers

`ES.FX.Primitives.Extensions.StringExtensions` also lives in the core package and provides `Truncate`,
`TruncateOrDefault`, `SplitIntoChunks`, `ToTitleCase`, and `RemoveDiacritics`. These are covered with the
other BCL-style helpers in [Core extensions](./core-extensions.md).

## See also

- [Results & Problems](./results-and-problems.md) — the `Result` / `Problem` error-handling primitives from the same core package.
- [Core extensions](./core-extensions.md) — BCL-style helpers, including the `StringExtensions` above.
- [Development](./index.md) — building, testing, and contributing to ES.FX.
