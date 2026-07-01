---
title: StackExchange.Redis additions
description: Helper extensions on IDatabase for reading the key prefix and deleting keys by pattern in batches.
---

## Overview

`ES.FX.Additions.StackExchange.Redis` augments the [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/) client with a small set of `IDatabase` extension methods. It fills two gaps the base client leaves to you: discovering the key prefix applied to a database (useful when a `ConfigurationOptions.ChannelPrefix` or keyspace convention is in play) and deleting many keys by pattern without blocking the server on a `KEYS *` scan.

Everything lives in one static class, `DatabaseExtensions`, so you keep using the standard `IConnectionMultiplexer` / `IDatabase` API and reach for these helpers only where you need them.

> [!TIP]
> Using Ignite? The [Redis client integration](../ignite/sparks/stackexchange-redis.md) registers an `IConnectionMultiplexer` with health checks and tracing already wired up. This Additions package is the raw helper layer with no Ignite dependency.

## Install

```bash
dotnet add package ES.FX.Additions.StackExchange.Redis
```

```xml
<PackageReference Include="ES.FX.Additions.StackExchange.Redis" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so an in-repo `<PackageReference>` carries no `Version` attribute. When you add this package to a standalone consumer that does not centralize versions, include `Version="…"`.

## What it adds

All members are extension methods on `StackExchange.Redis.IDatabase`, exposed through the static `DatabaseExtensions` class.

| Member | Signature | Purpose |
| --- | --- | --- |
| `GetKeyPrefix` | `RedisResult GetKeyPrefix(this IDatabase database)` | Returns the key prefix applied to the database (evaluated via a read-only Lua script). |
| `GetKeyPrefixAsync` | `Task<RedisResult> GetKeyPrefixAsync(this IDatabase database)` | Async form of `GetKeyPrefix`. |
| `KeysDelete` | `long KeysDelete(this IDatabase database, string pattern, int batchSize = 1000)` | Deletes all keys matching `pattern`, scanning and deleting in batches; returns the deleted count. |
| `KeysDeleteAsync` | `Task<long> KeysDeleteAsync(this IDatabase database, string pattern, int batchSize = 1000)` | Async form of `KeysDelete`. |
| `KeysDeleteAll` | `long KeysDeleteAll(this IDatabase database, int batchSize = 1000)` | Deletes every key (pattern `"*"`); returns the deleted count. |
| `KeysDeleteAllAsync` | `Task<long> KeysDeleteAllAsync(this IDatabase database, int batchSize = 1000)` | Async form of `KeysDeleteAll`. |

The `KeysDelete*` methods run a server-side Lua script that pages through the keyspace with `SCAN` and deletes each page as a batch, avoiding the O(N) blocking behavior of a single `KEYS`-then-`DEL` pass. Because the script is evaluated through the `IDatabase`, the pattern respects the database's key prefix (keyspace isolation) — a `WithKeyPrefix("tenant:")` database only ever matches and deletes its own keys. `batchSize` sets both the `SCAN … COUNT` page-size hint and the number of keys deleted per batch.

## Usage

Resolve an `IDatabase` from a multiplexer, then call the helpers directly.

```csharp
using StackExchange.Redis;
using ES.FX.Additions.StackExchange.Redis;

var multiplexer = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
IDatabase database = multiplexer.GetDatabase();
```

### Read the key prefix

```csharp
RedisResult prefix = await database.GetKeyPrefixAsync();
Console.WriteLine($"Key prefix: {prefix}");
```

### Delete keys by pattern

`KeysDeleteAsync` matches with a Redis glob pattern and returns how many keys were removed.

```csharp
long removed = await database.KeysDeleteAsync("session:*", batchSize: 500);
Console.WriteLine($"Removed {removed} session keys");
```

### Flush every key

`KeysDeleteAll` is the `"*"` shorthand — it deletes every key visible to the database.

```csharp
long removed = await database.KeysDeleteAllAsync();
```

> [!WARNING]
> `KeysDeleteAll` / `KeysDeleteAllAsync` delete **every** matching key on the database. Point them at a dedicated cache database and never at a shared or production data store you cannot afford to clear.

## Notes and limitations

- These are thin helpers over `IDatabase`. For connecting, configuring, pub/sub, transactions, and the full command surface, use [StackExchange.Redis](https://stackexchange.github.io/StackExchange.Redis/) directly — this package does not wrap or replace it.
- The `KeysDelete*` methods scan and delete via a server-side Lua script. The `pattern` argument uses Redis `SCAN … MATCH` glob syntax (`*`, `?`, `[…]`), not regular expressions. Because the script runs on a single node, they are intended for standalone/sentinel deployments, not Redis Cluster.
- The package registers no DI services and adds no Ignite wiring, health checks, or tracing. When you want a managed `IConnectionMultiplexer` with observability, use the [Redis client integration](../ignite/sparks/stackexchange-redis.md) Spark.

## See also

- [Redis client integration](../ignite/sparks/stackexchange-redis.md) — the Ignite Spark that registers `IConnectionMultiplexer` with health checks and tracing.
- [Additions](./index.md) — the full catalog of Additions packages.
- [StackExchange.Redis documentation](https://stackexchange.github.io/StackExchange.Redis/) — the upstream client this package augments.
