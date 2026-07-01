---
title: Newtonsoft.Json additions
description: A Newtonsoft.Json contract resolver that honors System.Text.Json's [JsonPropertyName] attribute.
---

## Overview

`ES.FX.Additions.Newtonsoft.Json` augments [Newtonsoft.Json](https://www.newtonsoft.com/json) with a
single contract resolver that lets Newtonsoft honor the `System.Text.Json`
[`[JsonPropertyName]`](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonpropertynameattribute)
attribute.

If you have model types annotated for `System.Text.Json` (the default serializer in modern ASP.NET Core)
but still need to serialize them with Newtonsoft.Json — for example a legacy library, a third-party API
client, or a mixed pipeline — Newtonsoft normally ignores `[JsonPropertyName]` and falls back to CLR
member names (or its own `[JsonProperty]`). `JsonPropertyNameContractResolver` closes that gap so both
serializers emit the same property names from one set of attributes.

## Install

```bash
dotnet add package ES.FX.Additions.Newtonsoft.Json
```

```xml
<PackageReference Include="ES.FX.Additions.Newtonsoft.Json" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so the `<PackageReference>` carries no `Version` attribute when
> the consuming project also centralizes versions. For a standalone consumer, add `Version="…"`.

## What it adds

| Type | Purpose |
| --- | --- |
| `JsonPropertyNameContractResolver : DefaultContractResolver` | A Newtonsoft.Json contract resolver that reads `System.Text.Json.Serialization.JsonPropertyNameAttribute` on each member and applies its `Name` as the serialized property name. |

The type lives in the `ES.FX.Additions.Newtonsoft.Json.Serialization` namespace and derives from
Newtonsoft's [`DefaultContractResolver`](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_Serialization_DefaultContractResolver.htm),
so it inherits all of Newtonsoft's default contract behavior and only overrides how property names are
resolved.

## Usage

Annotate your model with the `System.Text.Json` attribute:

```csharp
using System.Text.Json.Serialization;

public class Person
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = "";

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = "";
}
```

Assign the resolver to your Newtonsoft `JsonSerializerSettings`:

```csharp
using ES.FX.Additions.Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

var settings = new JsonSerializerSettings
{
    ContractResolver = new JsonPropertyNameContractResolver()
};

var json = JsonConvert.SerializeObject(
    new Person { FirstName = "Ada", LastName = "Lovelace" },
    settings);
// {"first_name":"Ada","last_name":"Lovelace"}
```

The same settings apply to deserialization:

```csharp
var person = JsonConvert.DeserializeObject<Person>(
    "{\"first_name\":\"Grace\",\"last_name\":\"Hopper\"}",
    settings);
```

To use it globally, set it as the default resolver:

```csharp
using ES.FX.Additions.Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    ContractResolver = new JsonPropertyNameContractResolver()
};
```

## Notes and limitations

- **Only `[JsonPropertyName]` is mapped.** The resolver reads `System.Text.Json`'s
  `JsonPropertyNameAttribute` and sets the resolved property name from it. Members without that attribute
  keep Newtonsoft's default naming. Other `System.Text.Json` attributes
  (`[JsonIgnore]`, `[JsonConverter]`, `[JsonPropertyOrder]`, and so on) are **not** interpreted — this
  package addresses property naming only.
- **Newtonsoft attributes still work.** Because it extends `DefaultContractResolver`, Newtonsoft's own
  attributes (`[JsonProperty]`, `[JsonIgnore]`) continue to behave as usual for anything the override does
  not touch. `[JsonPropertyName]` takes precedence over `[JsonProperty]` when both are present on the same
  member.
- **Reusable instance.** A single resolver instance is safe to share across serialization calls;
  Newtonsoft caches contracts internally, so prefer reusing one instance over allocating a new resolver
  per call.
- **No Ignite wiring.** This is a plain helper with no dependency on Ignite or DI. It does not register
  itself anywhere — you assign it to a `JsonSerializerSettings` yourself.

## See also

- [Newtonsoft.Json documentation](https://www.newtonsoft.com/json/help/html/Introduction.htm) — the base serializer API.
- [System.Text.Json additions](./system-text-json.md) — helpers for the built-in serializer.
- [Additions overview](./index.md) — the full catalog of ES.FX additions.
