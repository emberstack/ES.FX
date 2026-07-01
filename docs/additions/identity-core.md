---
title: Identity Core additions
description: Hash and verify passwords without an ASP.NET Core Identity user type using GenericPasswordHasher, a user-independent wrapper over IPasswordHasher.
---

## Overview

`ES.FX.Additions.Microsoft.Extensions.Identity.Core` augments [ASP.NET Core Identity's password hashing](https://learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/password-hashing) with a single helper: `GenericPasswordHasher`.

The framework type [`PasswordHasher<TUser>`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1) is generic over your user entity, and its `HashPassword`/`VerifyHashedPassword` methods take a `TUser` instance as their first argument. The built-in hasher ignores that user for its default algorithm, but the generic signature still forces you to have a user object on hand. `GenericPasswordHasher` closes that gap: it hashes and verifies passwords with no user parameter at all, so you can hash a password anywhere — a background job, a seeding routine, a domain type that is not an Identity user — without constructing a `TUser`.

This is a low-opinion Addition. It augments **only** `Microsoft.Extensions.Identity.Core`, registers nothing in DI on its own, and adds no configuration. For the underlying algorithm, options (`PasswordHasherOptions`, iteration count, compatibility mode), and the full Identity stack, see the upstream library.

## Install

```bash
dotnet add package ES.FX.Additions.Microsoft.Extensions.Identity.Core
```

```xml
<PackageReference Include="ES.FX.Additions.Microsoft.Extensions.Identity.Core" />
```

> [!NOTE]
> ES.FX uses Central Package Management. If your consuming project also centralizes versions, omit the `Version` attribute on the `<PackageReference>`; otherwise add `Version="…"`.

Installing this package brings in `Microsoft.Extensions.Identity.Core` (which provides `PasswordHasher<TUser>` and `PasswordVerificationResult`) transitively.

## What it adds

The package adds one public type in the `ES.FX.Additions.Microsoft.Extensions.Identity.Core.Passwords` namespace.

`GenericPasswordHasher` — a non-generic, user-independent password hasher backed by an internal `PasswordHasher<object>`.

| Member | Signature | Purpose |
| --- | --- | --- |
| `Instance` | `static GenericPasswordHasher Instance { get; }` | A shared, ready-to-use singleton instance. |
| `HashPassword` | `string HashPassword(string password)` | Returns a hashed representation of `password`. |
| `VerifyHashedPassword` | `PasswordVerificationResult VerifyHashedPassword(string hashedPassword, string providedPassword)` | Compares `providedPassword` against `hashedPassword` and returns a [`PasswordVerificationResult`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.passwordverificationresult). |

> [!NOTE]
> `GenericPasswordHasher` is a plain class with a public constructor, so you can also do `new GenericPasswordHasher()`. Use the `Instance` singleton unless you have a reason to hold your own; the type is stateless and safe to share.

## Usage

### Hash a password

Hash a password without any user object. The result is the standard ASP.NET Core Identity hash string (salt and metadata included) and is suitable for persistence.

```csharp
using ES.FX.Additions.Microsoft.Extensions.Identity.Core.Passwords;

string hash = GenericPasswordHasher.Instance.HashPassword("correct horse battery staple");
// store `hash` on your entity / record
```

### Verify a password

Compare a candidate password against a stored hash. `VerifyHashedPassword` returns a [`PasswordVerificationResult`](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.passwordverificationresult); a result of `Failed` means the passwords do not match, while `SuccessRehashNeeded` means they match but the stored hash used older parameters and should be re-hashed.

```csharp
using ES.FX.Additions.Microsoft.Extensions.Identity.Core.Passwords;
using Microsoft.AspNetCore.Identity;

var result = GenericPasswordHasher.Instance.VerifyHashedPassword(storedHash, providedPassword);

switch (result)
{
    case PasswordVerificationResult.Success:
        // authenticated
        break;
    case PasswordVerificationResult.SuccessRehashNeeded:
        // authenticated; re-hash and persist the new hash
        var refreshed = GenericPasswordHasher.Instance.HashPassword(providedPassword);
        break;
    case PasswordVerificationResult.Failed:
        // reject
        break;
}
```

### Register it in DI (optional)

The Addition does not touch DI, but the shared instance is easy to register yourself when you want it injected.

```csharp
using ES.FX.Additions.Microsoft.Extensions.Identity.Core.Passwords;

builder.Services.AddSingleton(GenericPasswordHasher.Instance);
```

```csharp
public sealed class AccountService(GenericPasswordHasher passwordHasher)
{
    public string HashNewPassword(string password) => passwordHasher.HashPassword(password);
}
```

## Notes and limitations

- **Same algorithm as the framework hasher.** `GenericPasswordHasher` delegates to `PasswordHasher<object>`, so hashes are produced and verified by the standard ASP.NET Core Identity algorithm with its default parameters. A hash created here interoperates with `PasswordHasher<TUser>` and vice versa.
- **No options.** The wrapped hasher uses its defaults; there is no overload to pass `PasswordHasherOptions` (compatibility mode, iteration count). If you need to tune those, use `PasswordHasher<TUser>` directly with an `IOptions<PasswordHasherOptions>`.
- **User-independent by design.** Because it hashes against a fixed placeholder rather than your user entity, do not use it if you rely on a custom `IPasswordHasher<TUser>` whose behavior depends on the specific user.
- **Not a DI registration.** The package registers nothing. Use `GenericPasswordHasher.Instance` directly, or register it yourself as shown above.

## See also

- [Additions catalog](./index.md) — the full set of ES.FX Additions.
- [ASP.NET Core password hashing](https://learn.microsoft.com/aspnet/core/security/data-protection/consumer-apis/password-hashing) — the upstream hashing guidance.
- [`PasswordHasher<TUser>` API](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1) — the framework type this helper wraps.
- [Microsoft.Extensions.Identity.Core](https://learn.microsoft.com/aspnet/core/security/authentication/identity) — the single dependency this Addition augments.
