---
title: FluentValidation additions
description: Convert FluentValidation results into ES.FX ValidationProblem and error dictionaries so validation failures flow through the Result/Problem model.
---

## Overview

`ES.FX.Additions.FluentValidation` bridges [FluentValidation](https://docs.fluentvalidation.net/) with the ES.FX core [`Problem`/`Result` model](../development/results-and-problems.md). It adds two extension methods on FluentValidation's `ValidationResult` that turn a set of validation failures into shapes the rest of ES.FX already understands:

- a flat `IDictionary<string, string[]>` of field → messages (the same shape ASP.NET Core uses for `ValidationProblemDetails`), and
- a [`ValidationProblem`](../development/results-and-problems.md), which implicitly converts to a [`Result`](../development/results-and-problems.md).

That means a failed `IValidator<T>.Validate(...)` call can be returned straight through your `Result<T>`-based APIs without hand-rolling the mapping. This package is a low-opinion Addition: it augments **only** FluentValidation and does not register anything in DI. For DI-wired validator discovery and Ignite integration, see the [FluentValidation Spark](../ignite/sparks/fluentvalidation.md).

## Install

```bash
dotnet add package ES.FX.Additions.FluentValidation
```

```xml
<PackageReference Include="ES.FX.Additions.FluentValidation" />
```

> [!NOTE]
> ES.FX uses Central Package Management. If your consuming project also centralizes versions, omit the `Version` attribute on the `<PackageReference>`; otherwise add `Version="…"`.

Installing this package brings in `FluentValidation` and `ES.FX` (for `ValidationProblem`) transitively.

## What it adds

Both methods are extension methods on FluentValidation's `ValidationResult` (the type returned by `IValidator<T>.Validate(...)`).

| Extension method | Signature | Purpose |
| --- | --- | --- |
| `ToValidationErrors` | `IDictionary<string, string[]> ToValidationErrors(this ValidationResult validationResult)` | Groups the result's `Errors` by `PropertyName` into a dictionary of field name → error messages. |
| `ToValidationProblem` | `ValidationProblem ToValidationProblem(this ValidationResult validationResult)` | Wraps the grouped errors in an ES.FX [`ValidationProblem`](../development/results-and-problems.md). |

`ToValidationErrors` lives in the `ES.FX.Additions.FluentValidation.Results` namespace; `ToValidationProblem` lives in `ES.FX.Additions.FluentValidation.Problems`. `ToValidationProblem` is built on top of `ToValidationErrors`.

> [!NOTE]
> Multiple failures on the same property are collapsed into a single dictionary entry whose value array holds every message for that property. A `ValidationResult` with no errors (i.e. `IsValid == true`) yields an empty dictionary and an empty `ValidationProblem`.

## Usage

### Convert a validation result to a Problem

Validate an instance, and when it fails, hand the failures to the caller as a `ValidationProblem`.

```csharp
using ES.FX.Additions.FluentValidation.Problems;
using ES.FX.Problems;
using FluentValidation;

public sealed class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
    }
}

var validator = new CreateUserValidator();
var result = validator.Validate(new CreateUser { Email = "", Age = 12 });

if (!result.IsValid)
{
    ValidationProblem problem = result.ToValidationProblem();
    // problem.Errors["Email"] -> ["'Email' must not be empty.", ...]
    // problem.Errors["Age"]   -> ["'Age' must be greater than or equal to 18."]
}
```

### Return it through the Result model

Because [`ValidationProblem`](../development/results-and-problems.md) derives from `Problem`, it converts implicitly to a [`Result`](../development/results-and-problems.md) or `Result<T>`. This lets a validating method return either the validated value or the problem from a single method, with no explicit wrapping.

```csharp
using ES.FX.Additions.FluentValidation.Problems;
using ES.FX.Results;

public Result<User> CreateUser(CreateUser command)
{
    var result = _validator.Validate(command);
    if (!result.IsValid)
        return result.ToValidationProblem(); // ValidationProblem -> Problem -> Result<User>

    return _repository.Add(command); // User -> Result<User>
}
```

The caller inspects the outcome with the `Result<T>` API:

```csharp
Result<User> outcome = CreateUser(command);

if (outcome.TryPickProblem(out var problem, out var user))
{
    // problem is the ValidationProblem when validation failed
}
else
{
    // user is the created User
}
```

### Use just the error dictionary

When you only need the ASP.NET Core-style dictionary (for example to feed a custom response), call `ToValidationErrors` directly.

```csharp
using ES.FX.Additions.FluentValidation.Results;

IDictionary<string, string[]> errors = _validator
    .Validate(command)
    .ToValidationErrors();
```

## Notes and limitations

- **No DI, no validator discovery.** This Addition only maps an existing `ValidationResult`. It does not scan assemblies, register `IValidator<T>` implementations, or hook into ASP.NET Core's model-binding pipeline. Use the [FluentValidation Spark](../ignite/sparks/fluentvalidation.md) for Ignite-wired validator registration.
- **You run the validator.** These methods start from a `ValidationResult`, so you call `Validate`/`ValidateAsync` yourself and then map the outcome.
- **Property names are the dictionary keys.** Keys come verbatim from `ValidationFailure.PropertyName` (which reflects FluentValidation's configured property-name resolution, including nested paths like `Address.City`).
- **Only messages are carried over.** `ToValidationErrors` keeps `PropertyName` and `ErrorMessage`; it does not surface severity, error codes, attempted values, or custom state from the underlying `ValidationFailure`.

## See also

- [Results and Problems](../development/results-and-problems.md) — the ES.FX `Result`/`Problem` model these methods target, and how a `ValidationProblem` flows back to callers.
- [FluentValidation Spark](../ignite/sparks/fluentvalidation.md) — Ignite-wired validator registration and configuration.
- [Additions catalog](./index.md) — the full set of ES.FX Additions.
- [FluentValidation documentation](https://docs.fluentvalidation.net/) — the upstream library.
