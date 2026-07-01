---
title: System.Text.Json additions
description: Exception-safe deserialization helpers, preconfigured JsonSerializerOptions, and extra JSON converters for System.Text.Json.
---

Additions for [System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/), the built-in .NET JSON serializer. This package (`ES.FX.Additions.System.Text.Json`) layers a small set of convenience helpers on top of it — nothing more.

## Overview

`System.Text.Json` gives you `JsonSerializer`, but its `Deserialize` methods throw on malformed input and it ships no preconfigured `JsonSerializerOptions`. This Addition fills those gaps with:

- **Try/OrDefault deserialization helpers** on `string` and `Stream` that never throw — they return `false` or a fallback value instead.
- **`ConvertViaJson` / `TryConvertViaJson`** for round-tripping one object into another type through JSON.
- **`JsonSerializerOptionsExtended`** — three ready-made `JsonSerializerOptions` presets (`WebApi`, `JavascriptWebApi`, `Payload`).
- **Custom `JsonConverter`s** for lenient booleans and Unix-time `DateTimeOffset` values.

Everything else — the actual serialization semantics, attributes, source generation — is stock `System.Text.Json`. See the [upstream documentation](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/) for the base API.

> [!NOTE]
> This is a plain helper library, not an Ignite Spark. There is no DI registration and no `Ignite{Service}` entry point — you call the static helpers directly.

## Install

```bash
dotnet add package ES.FX.Additions.System.Text.Json
```

```xml
<PackageReference Include="ES.FX.Additions.System.Text.Json" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so the `<PackageReference>` carries no `Version` attribute when the consuming project also centralizes versions. For a standalone consumer, add `Version="…"`.

## What it adds

All helpers live in the `ES.FX.Additions.System.Text.Json.Serialization` namespace (converters are under `.Serialization.Converters`).

### Deserialization helpers (`JsonSerializerExtensions`)

Static extension methods. When `options` is `null`, they fall back to `JsonSerializerOptionsExtended.WebApi`.

| Member | Purpose |
| --- | --- |
| `bool TryJsonDeserialize<T>(this string? utf8Json, out T? result, JsonSerializerOptions? options = null)` | Deserialize a JSON string; returns `false` (no throw) on null input or failure. |
| `bool TryJsonDeserialize<T>(this Stream? utf8Json, out T? result, JsonSerializerOptions? options = null)` | Same, from a UTF-8 stream. |
| `T? JsonDeserializeOrDefault<T>(this string? utf8Json, T? defaultValue = default, JsonSerializerOptions? options = null)` | Deserialize a JSON string, returning `defaultValue` on null input or failure. |
| `T? JsonDeserializeOrDefault<T>(this Stream? utf8Json, T? defaultValue = default, JsonSerializerOptions? options = null)` | Same, from a UTF-8 stream. |
| `T? ConvertViaJson<T>(this object? source, T? defaultValue = default, JsonSerializerOptions? options = null)` | Convert any object to `T` by serializing then deserializing; returns `defaultValue` on failure. If `source` is already a `string`, it is treated as JSON. |
| `bool TryConvertViaJson<T>(this object? source, out T? result, JsonSerializerOptions? options = null)` | Same conversion, but returns `false` (no throw) on failure. |

> [!NOTE]
> The `Try*` methods annotate `result` with `[NotNullWhen(true)]`, and the `*OrDefault` / `ConvertViaJson` methods use `[NotNullIfNotNull(nameof(defaultValue))]`, so nullable analysis flows correctly through them.

### Preconfigured options (`JsonSerializerOptionsExtended`)

Static, singleton `JsonSerializerOptions` instances. Each includes a `JsonStringEnumConverter`.

| Member | Base defaults | Notes |
| --- | --- | --- |
| `JsonSerializerOptions WebApi { get; }` | `JsonSerializerDefaults.Web` | String-enum converter using `JsonSerializerOptions.Default.PropertyNamingPolicy`. The fallback used by every helper above. |
| `JsonSerializerOptions JavascriptWebApi { get; }` | `JsonSerializerDefaults.Web` | String-enum converter using `JsonSerializerOptions.Web.PropertyNamingPolicy` (camelCase). |
| `JsonSerializerOptions Payload { get; }` | `JsonSerializerDefaults.General` | `PropertyNameCaseInsensitive = true` plus a string-enum converter. |

> [!WARNING]
> These are shared singleton instances that are **sealed read-only** (via `MakeReadOnly()`), so attempting to mutate their `Converters` collection or properties at runtime throws `InvalidOperationException`. To customize, copy-construct a new instance (`new JsonSerializerOptions(JsonSerializerOptionsExtended.WebApi)`) and mutate the copy.

### Converters (`Serialization.Converters`)

Add these to a `JsonSerializerOptions.Converters` collection or via `[JsonConverter(typeof(...))]`.

| Converter | Handles | Behavior |
| --- | --- | --- |
| `BooleanConverter : JsonConverterFactory` | `bool` and `bool?` | Lenient read: accepts JSON `true`/`false`, strings (`"true"`, `"false"`, `"1"`, `"0"`), and numbers (`1`/`0`). For `bool?`, null or whitespace-only strings read as `null`; for non-nullable `bool` they throw `JsonException`. Any other value throws `JsonException`. |
| `UnixTimeSecondsDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>` | `DateTimeOffset` | Reads Unix time in **seconds** from a number or numeric string; writes a number. Null/empty throws. |
| `UnixTimeSecondsNullableDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset?>` | `DateTimeOffset?` | Same, but null/empty reads as `null` and `null` writes as JSON null. |
| `UnixTimeMillisecondsDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>` | `DateTimeOffset` | Reads Unix time in **milliseconds**; writes a number. Null/empty throws. |
| `UnixTimeMillisecondsNullableDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset?>` | `DateTimeOffset?` | Same, but null/empty reads as `null` and `null` writes as JSON null. |

## Usage

### Deserialize without try/catch

```csharp
using ES.FX.Additions.System.Text.Json.Serialization;

var json = """{ "name": "Ada", "active": true }""";

if (json.TryJsonDeserialize<Person>(out var person))
{
    // person is guaranteed non-null here
    Console.WriteLine(person.Name);
}

// Or with a fallback instead of a bool
var personOrDefault = json.JsonDeserializeOrDefault(new Person());
```

The same overloads work on a `Stream`:

```csharp
await using var stream = File.OpenRead("person.json");
if (stream.TryJsonDeserialize<Person>(out var person))
{
    // …
}
```

### Convert between compatible types

`ConvertViaJson` serializes the source and deserializes it as the target type — handy for mapping between DTOs with matching JSON shapes:

```csharp
using ES.FX.Additions.System.Text.Json.Serialization;

PersonDto dto = GetDto();
var summary = dto.ConvertViaJson<PersonSummary>();

// Or the non-throwing form
if (dto.TryConvertViaJson<PersonSummary>(out var summary2))
{
    // …
}
```

### Use a preconfigured options preset

Pass a preset explicitly, or rely on the `WebApi` default that the helpers use when `options` is `null`:

```csharp
using System.Text.Json;
using ES.FX.Additions.System.Text.Json.Serialization;

// Explicit preset
var payload = JsonSerializer.Serialize(order, JsonSerializerOptionsExtended.Payload);

// Helpers default to JsonSerializerOptionsExtended.WebApi when no options are supplied
json.TryJsonDeserialize<Order>(out var order);
```

### Apply a converter

Register a converter on the options you use, or annotate the member directly:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using ES.FX.Additions.System.Text.Json.Serialization.Converters;

var options = new JsonSerializerOptions
{
    Converters =
    {
        new BooleanConverter(),
        new UnixTimeSecondsNullableDateTimeOffsetJsonConverter()
    }
};

// "1" and 1 both deserialize to true; a "1700000000" epoch string becomes a DateTimeOffset
var record = JsonSerializer.Deserialize<Record>(input, options);
```

```csharp
public sealed class Record
{
    [JsonConverter(typeof(BooleanConverter))]
    public bool? Enabled { get; set; }

    [JsonConverter(typeof(UnixTimeSecondsDateTimeOffsetJsonConverter))]
    public DateTimeOffset CreatedAt { get; set; }
}
```

## Notes and limitations

- **Not an Ignite Spark.** No DI wiring, no health checks, no OpenTelemetry — just static helpers and converters you call directly.
- **The `Try*` and `*OrDefault` helpers swallow all serialization exceptions.** They catch every `Exception` thrown during (de)serialization and return `false`/`defaultValue`. The source-generated overloads that take a `JsonTypeInfo` are the one exception: passing `null` metadata throws `ArgumentNullException` (a usage bug, not a parse failure). If you need to distinguish a null input from a genuine parse error, call `JsonSerializer.Deserialize` yourself.
- **`WebApi` is the implicit default.** Any helper called with `options: null` uses `JsonSerializerOptionsExtended.WebApi` — be aware of its `JsonSerializerDefaults.Web` semantics (camelCase, case-insensitive property matching) when you omit options.
- **Presets are shared singletons.** Treat them as read-only (see the warning above).
- **The non-nullable Unix-time converters throw on null/empty.** Use the `Nullable` variants for `DateTimeOffset?` members that may be absent.

## See also

- [System.Text.Json documentation](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/) — the upstream serializer.
- [Custom converters in System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/converters-how-to) — how converters are applied.
- [Newtonsoft.Json additions](./newtonsoft-json.md) — the equivalent helpers for the Newtonsoft serializer.
- [Additions overview](./index.md) — the full Additions catalog.
