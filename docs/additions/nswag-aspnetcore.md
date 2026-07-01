---
title: NSwag.AspNetCore additions
description: A drop-in NJsonSchema schema-name generator that names OpenAPI schemas by their full CLR type name.
---

## Overview

`ES.FX.Additions.NSwag.AspNetCore` augments [NSwag.AspNetCore](https://github.com/RicoSuter/NSwag) with a
single helper: a schema-name generator that produces OpenAPI schema names from the full CLR type name
instead of NJsonSchema's default short name.

The default `ISchemaNameGenerator` names schemas by the simple type name (e.g. `Order`). That collides
when two types in different namespaces share a name, and it loses generic type arguments. Assigning
`TypeToStringSchemaNameGenerator` uses `Type.ToString()` — the namespace-qualified, generic-aware name —
so every schema gets a stable, unique identifier.

> [!TIP]
> Using Ignite? The [NSwag Spark](../ignite/sparks/nswag.md) wires NSwag's middleware into the Ignite
> two-phase model (`app.IgniteNSwag()`). This Addition supplies the schema-name generator you plug into
> your `AddOpenApiDocument` registration; the two are complementary.

## Install

```bash
dotnet add package ES.FX.Additions.NSwag.AspNetCore
```

```xml
<PackageReference Include="ES.FX.Additions.NSwag.AspNetCore" />
```

> [!NOTE]
> ES.FX uses Central Package Management, so an in-repo `<PackageReference>` carries no `Version`
> attribute. In a standalone consumer that does not centralize versions, add `Version="…"`.

## What it adds

| Type | Member | Purpose |
| --- | --- | --- |
| `TypeToStringSchemaNameGenerator` | `string Generate(Type type)` | Implements NJsonSchema's `ISchemaNameGenerator`; returns `type.ToString()` as the schema name. |
| `SanitizedSchemaNameGenerator` | `string Generate(Type type)` | Implements NJsonSchema's `ISchemaNameGenerator`; returns a sanitized `type.ToString()` that always matches the OpenAPI component key pattern `^[a-zA-Z0-9.\-_]+$`. |

Namespace: `ES.FX.Additions.NSwag.AspNetCore.Generation`.

Both generators implement `NJsonSchema.Generation.ISchemaNameGenerator` (from NJsonSchema, an NSwag
dependency), so you assign an instance wherever NSwag exposes the schema settings — namely
`settings.SchemaSettings.SchemaNameGenerator`.

`TypeToStringSchemaNameGenerator` returns `Type.ToString()` verbatim, which favours uniqueness but can
emit characters — backtick arity, brackets, commas, `+` — that fall outside the OpenAPI component key
pattern. `SanitizedSchemaNameGenerator` keeps the same descriptive shape but replaces every disallowed
character with an underscore (collapsing consecutive replacements and trimming leading/trailing
underscores), so the produced name always matches `^[a-zA-Z0-9.\-_]+$`. For example,
`System.Collections.Generic.List`1[System.String]` becomes
`System.Collections.Generic.List_1_System.String`.

## Usage

Assign the generator when registering an OpenAPI document with NSwag's `AddOpenApiDocument`:

```csharp
using ES.FX.Additions.NSwag.AspNetCore.Generation;

builder.Services.AddOpenApiDocument(settings =>
{
    settings.DocumentName = "latest";
    settings.SchemaSettings.SchemaNameGenerator = new TypeToStringSchemaNameGenerator();
});
```

When you need OpenAPI-valid schema keys, swap in `SanitizedSchemaNameGenerator`, which sanitizes the
same `Type.ToString()` output so every name matches `^[a-zA-Z0-9.\-_]+$`:

```csharp
using ES.FX.Additions.NSwag.AspNetCore.Generation;

builder.Services.AddOpenApiDocument(settings =>
{
    settings.DocumentName = "latest";
    settings.SchemaSettings.SchemaNameGenerator = new SanitizedSchemaNameGenerator();
});
```

The generator is stateless and thread-safe, so a single instance can be reused across multiple document
registrations:

```csharp
using ES.FX.Additions.NSwag.AspNetCore.Generation;

var schemaNameGenerator = new TypeToStringSchemaNameGenerator();

foreach (var documentName in new[] { "v1", "v2", "latest" })
{
    builder.Services.AddOpenApiDocument(settings =>
    {
        settings.DocumentName = documentName;
        settings.SchemaSettings.SchemaNameGenerator = schemaNameGenerator;
    });
}
```

## Notes and limitations

- This package adds **only** the schema-name generator. Everything else — registering documents
  (`AddOpenApiDocument`), serving the JSON, and the Swagger/ReDoc UI — is standard NSwag.AspNetCore.
  See the [NSwag documentation](https://github.com/RicoSuter/NSwag/wiki) for the base API.
- Schema names come straight from `Type.ToString()`, so they include the full namespace and generic
  type arguments. This favours uniqueness over brevity; expect longer, fully-qualified names in the
  generated OpenAPI document. Note the exact `Type.ToString()` formatting: generic types include
  backtick arity and bracketed type arguments (e.g. `System.Collections.Generic.List`1[System.String]`)
  and nested types include a `+` separator — characters that may not conform to the OpenAPI component
  key pattern.
- The generator has no ES.FX or Ignite dependency — it works in any project that uses NSwag.AspNetCore.
  For the full Ignite integration (middleware wiring, health, and the `app.IgniteNSwag()` post-build
  step), use the paired Spark instead.

## See also

- [NSwag Spark](../ignite/sparks/nswag.md) — the Ignite integration that pairs with this Addition.
- [Swashbuckle Spark](../ignite/sparks/swashbuckle.md) — the alternative OpenAPI/Swagger toolchain.
- [Additions catalog](./index.md) — all ES.FX Additions.
- [NSwag](https://github.com/RicoSuter/NSwag) and [NJsonSchema](https://github.com/RicoSuter/NJsonSchema) — the upstream libraries.
