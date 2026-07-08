---
title: OpenData
description: The ES.FX.OpenData family of baked-in reference-data libraries — countries and Romanian SIRUTA administrative units — with a fluent hub, clean display names, and diacritic-insensitive search.
---

`ES.FX.OpenData` is a family of libraries that ship **baked-in public reference data** as embedded resources:
country codes, and the Romanian SIRUTA administrative-territorial register. Each dataset is a standalone
NuGet package that parses its embedded, edition-stamped data once, freezes it into immutable indexes, and
serves it through a small typed interface — no configuration, no I/O, no third-party parser dependencies.

Datasets are consumed two ways: inject the specific dataset interface directly (the primary path), or inject
the `IOpenData` hub and navigate a fluent, discoverable surface. Both resolve the same singleton instances.

## The packages

| Package | Interface | What it gives you |
| --- | --- | --- |
| `ES.FX.OpenData` | `IOpenData` | Core: the hub, the registration builder, edition metadata (`OpenDatasetInfo`), the warmup service, and the embedded-resource loaders. Ships no data. |
| `ES.FX.OpenData.Countries` | `ICountriesDataset` | ISO 3166-1 countries: alpha-2/alpha-3/numeric codes, English + localized names, and alias resolution (`CYP`, `US-HI`, …). |
| `ES.FX.OpenData.Romania.AdministrativeUnits` | `IRomanianAdministrativeUnitsDataset` | The Romanian SIRUTA register — counties, UATs, and localities — with clean title-cased display names, diacritic-insensitive search, postal codes, and national ID series. |
| `ES.FX.OpenData.Vies` | `IViesClient` | A typed client for the EU VIES VAT-number validation service, over `IHttpClientFactory` — a tri-state result and typed faults. |
| `ES.FX.OpenData.Romania.Fiscal.Anaf` | `IAnafClient` | A typed, batch-native client for the Romanian ANAF VAT-payer registry (PlatitorTvaRest v9), with a built-in per-process request throttle. |

The family has two kinds of member: **datasets** (baked-in, synchronous, edition-stamped) and **clients**
(live open-government APIs, asynchronous, over `IHttpClientFactory`).

> [!NOTE]
> A worldwide subdivisions dataset (`ES.FX.OpenData.Countries.Subdivisions`, ISO 3166-2) is planned but not yet
> shipped.

## Registration

`AddOpenData()` returns a builder; each dataset package contributes one flat `Add{Scope}{Leaf}()` method.
Registration is idempotent.

```csharp
services.AddOpenData()                       // or AddOpenData(o => o.WarmupMode = OpenDataWarmupMode.Blocking)
    .AddCountries()
    .AddRomaniaAdministrativeUnits();
```

Each `Add…` registers its dataset as a singleton and contributes an `OpenDatasetInfo` used by diagnostics and
the startup **warmup** service, which materializes every dataset off the hot path (background by default; set
`WarmupMode` to `Blocking` to guarantee readiness before traffic, or `None` to stay purely lazy).

## Consumption

**Door 1 — inject the dataset you need** (recommended for most services):

```csharp
public sealed class AddressValidator(IRomanianAdministrativeUnitsDataset units)
{
    public bool IsKnownLocality(int sirutaCode) => units.Find(sirutaCode) is { Level: 3 };
}
```

**Door 2 — the hub**, for cross-dataset work and diagnostics. Each package hangs a fluent accessor off
`IOpenData` (a C# extension member), lit up by the single `using ES.FX.OpenData;`:

```csharp
openData.Countries["RO"].GetLocalizedName(new CultureInfo("ro"));   // "România"
openData.RomaniaAdministrativeUnits.Localities.Search("alba iulia").First().SirutaCode;
openData.Datasets;   // every installed dataset + its edition, for logging / health
```

### Lookup contract (family-wide)

Every dataset follows the same shape, matching BCL dictionary conventions:

- `this[key]` — returns the value or **throws** `KeyNotFoundException` (use for known-constant keys).
- `Find(key)` — returns the value or `null`.
- `TryGet(key, out value)` — returns `bool`.

## Countries

```csharp
ICountriesDataset countries = /* injected */;

countries["RO"].Name;              // "Romania" (English short name)
countries.ByNumericCode(642);      // Romania
countries.Resolve("CYP");          // Northern Cyprus — alias resolves to canonical CY codes, territory name
countries.Find("CYP");             // null — aliases never pollute the canonical list
```

`GetLocalizedName(CultureInfo)` walks the culture's parent chain and falls back to the English `Name`. The
base package guarantees `en` and `ro`.

## Romania — SIRUTA administrative units

The raw SIRUTA `DENLOC` column is ALL CAPS with legacy cedilla diacritics. This dataset serves **clean
names**: `Name`/`DisplayName` are title-cased with modern comma-below diacritics (`Bărăști`, `Timișoara`), and
villages belonging to a commune get a disambiguating `DisplayName` (`"Bărăști (Albac)"`). `NormalizedName` is
the folded search key (diacritics stripped, hyphens spaced, lower-cased).

```csharp
IRomanianAdministrativeUnitsDataset ro = /* injected */;

ro[1026].DisplayName;              // "Alba Iulia"
ro.Find(10);                       // JUDEȚUL ALBA — resolves at ANY level, never throws
ro.GetLocalitiesInCounty("CJ");    // localities of Cluj county (also accepts "RO-CJ")
ro.Counties;                       // 42 counties, enriched with ISO code + national ID series
ro.Search("cluj-napoca");          // diacritic-, case-, and hyphen-insensitive
```

`Search` folds the query the same way stored names are folded, so `"cluj-napoca"`, `"cluj napoca"`,
`"CLUJ-NAPOCA"`, `"timiș"` and `"timis"` all match as expected, and results are deterministically ordered.

## Clients (VIES, ANAF)

Clients register on the same builder and are injected directly (`IViesClient` / `IAnafClient`). Each is a
**singleton over `IHttpClientFactory`** (safe to inject into consumers of any lifetime). No resilience handler
is applied by default — chain one via the `configureHttpClient` escape hatch, or rely on your host's defaults.

```csharp
services.AddOpenData()
    .AddVies()
    .AddRomaniaAnaf(o => o.RequestsPerSecond = 1);   // ANAF throttles per source IP
```

**VIES** returns a tri-state result — a member state being unavailable is a *value*, not an exception. Only
genuine faults throw `ViesApiException` (or `ArgumentException` for rejected input):

```csharp
var result = await vies.ValidateAsync("RO", "12345678", ct);
// result.Status ∈ { Valid, Invalid, MemberStateUnavailable }
```

**ANAF** is batch-native. `FindCompanyAsync` returns `null` when ANAF doesn't recognize a CUI (an outcome,
not a fault); `FindCompaniesAsync` splits results into found/not-found and chunks large inputs through the
throttle. CUIs are `long`. ANAF's lookup is an idempotent query performed via `POST` — do not disable
unsafe-method retries for this client if you add a resilience handler.

```csharp
var company = await anaf.FindCompanyAsync(123456, asOf: null, ct);        // null if not found
var batch   = await anaf.FindCompaniesAsync([123, 456, 789], asOf: null, ct); // batch.Found / batch.NotFound
```

Both clients validate their options at host startup (`ValidateOnStart`) and emit an OpenTelemetry span per
operation on an `ActivitySource` named after the package — add `"ES.FX.OpenData.Vies"` /
`"ES.FX.OpenData.Romania.Fiscal.Anaf"` to your tracer to capture them.

> [!NOTE]
> These clients are plain libraries with no Ignite Spark — there is nothing to configure beyond a base URL and
> (for ANAF) the request budget.

## Editions & provenance

Each dataset carries an `OpenDatasetInfo` (`Name`, `Edition`, `Source`, `License`, `Standard`), surfaced at
runtime via `IOpenData.Datasets`. The data is embedded and edition-stamped; a change of edition is always at
least a minor package-version bump (never a patch), so consumers that persist codes can gate on `Edition`.

- **Countries** — derived from the public ISO 3166-1 country code list.
- **SIRUTA** — published by INS (Institutul Național de Statistică). Current edition: `2025-12`. The data is
  canonicalized at ingest (NFC, legacy cedilla → comma-below) and embedded gzip-compressed (~260 KB).

## See also

- [Framework libraries](./index.md)
- [Creating a new ES.FX library](../development/creating-a-library.md)
- [Primitives](../development/primitives.md) — the `ToTitleCase` / `RemoveDiacritics` helpers the datasets build on.
