---
title: OpenData
description: The ES.FX.OpenData family of baked-in reference-data libraries — ISO 3166 codes, curated countries, and Romanian SIRUTA territorial units — plus typed VIES/ANAF clients. Each package self-registers with a plain services.AddX(); clean display names and diacritic-insensitive search.
---

`ES.FX.OpenData` is a family of libraries that ship **baked-in public reference data** as embedded resources:
ISO 3166 codes, curated country data, and the Romanian SIRUTA administrative-territorial register. Each
dataset is a standalone NuGet package that parses its embedded, edition-stamped data once, freezes it into
immutable indexes, and serves it through a small typed interface — no configuration, no I/O, no third-party
parser dependencies. The family also includes two typed clients (VIES, ANAF) over live open-government APIs.

Every package registers itself with a plain `services.AddX()` extension on `IServiceCollection` and is
consumed by **injecting its dataset/client interface directly**. There is no shared hub or builder to set up
first — install a package, call its `Add…`, inject its interface.

## The packages

| Package | Interface | What it gives you |
| --- | --- | --- |
| `ES.FX.OpenData.Countries` | `ICountriesDataset`, `ICountrySubdivisionsDataset`; and (namespace `ES.FX.OpenData.Countries.ISO3166`) `IIso3166Countries`, `IIso3166CountrySubdivisions`, `IIso3166FormerCountries` (+ `IIso3166` aggregate) | Curated ISO 3166-1 countries (alpha-2/alpha-3/numeric codes, English + localized names, guaranteed `en` and `ro`) and their ISO 3166-2 subdivisions as localizable `CountrySubdivision` objects — plus the **raw ISO 3166 datasets** (parts 1/2/3, registered via `services.AddIso3166()`) that the curated data is built on. Ships generated `CountryAlpha2Codes` / `CountryAlpha3Codes` / `CountryNumericCodes` (name-keyed) and `CountryCodes` (keyed by the alpha-2 code itself) constant classes. |
| `ES.FX.OpenData.Currencies` | `ICurrenciesDataset`; and (namespace `ES.FX.OpenData.Currencies.ISO4217`) `IIso4217Currencies` | ISO 4217 currencies (alpha-3 + numeric codes, English name, localized names with `en` guaranteed) via `services.AddCurrencies()` — plus the **raw ISO 4217 dataset** (registered via `services.AddIso4217()`) it is built on. Ships generated `CurrencyAlpha3Codes` / `CurrencyNumericCodes` (name-keyed) and `CurrencyCodes` (keyed by the alpha-3 code itself) constant classes. |
| `ES.FX.OpenData.Romania.TerritorialUnits` | `IRomanianTerritorialUnitsDataset` | The Romanian SIRUTA register — counties, UATs, and localities — with clean title-cased display names, diacritic-insensitive search, and postal codes. County ISO 3166-2 identity is sourced from the ISO 3166-2 dataset in `ES.FX.OpenData.Countries`. |
| `ES.FX.OpenData.Vies` | `IViesClient` | A typed client for the EU VIES VAT-number validation service, over `IHttpClientFactory` — a tri-state result and typed faults. |
| `ES.FX.OpenData.Romania.Anaf` | `IAnafVatCheckClient` | A typed, batch-native client for the Romanian ANAF VAT-payer registry (PlatitorTvaRest v9), with a built-in per-process request throttle; resolves each address to its SIRUTA locality, UAT, and county through an embedded ANAF→SIRUTA crosswalk (references `ES.FX.OpenData.Romania.TerritorialUnits`). |

The family has two kinds of member: **datasets** (baked-in, synchronous, edition-stamped) and **clients**
(live open-government APIs, asynchronous, over `IHttpClientFactory`).

## Registration

Each package contributes one flat `Add{Scope}{Leaf}()` extension on `IServiceCollection`, in the conventional
`Microsoft.Extensions.DependencyInjection` namespace (so no extra `using` is needed). Every `Add…` returns
`IServiceCollection`, so registrations chain, and each is **idempotent** — safe to call more than once, and
inter-package dependencies register themselves. **`AddCountries()` is the library umbrella** — one call
registers every dataset in the package (curated countries + subdivisions **and** all three ISO 3166 datasets +
the `IIso3166` aggregate). Use the granular `AddCountrySubdivisions()` / `AddIso3166()` (or `AddIso3166Countries()`
/ `AddIso3166CountrySubdivisions()` / `AddIso3166FormerCountries()`) methods to register a single dataset instead.

```csharp
services
    .AddCountries()                // curated countries + subdivisions + all ISO 3166 datasets
    .AddRomaniaTerritorialUnits()  // SIRUTA (sources ISO 3166-2 automatically)
    .AddVies()
    .AddRomaniaAnaf(o => o.RequestsPerSecond = 1);
```

### Startup warmup (optional, per-package)

Datasets materialize **lazily** on first access. The one dataset heavy enough to warrant eager loading —
SIRUTA (~17k rows) — offers an opt-in that registers a hosted service to materialize it at host startup, so a
corrupt embedded resource surfaces at boot rather than on the first request:

```csharp
services.AddRomaniaTerritorialUnits(warmup: true);
```

## Consumption

Inject the dataset (or client) interface you need — that is the whole story:

```csharp
public sealed class AddressValidator(IRomanianTerritorialUnitsDataset units)
{
    public bool IsKnownLocality(int sirutaCode) => units.Find(sirutaCode) is { Level: 3 };
}
```

For grouped access to the three ISO 3166 parts, inject the **`IIso3166` aggregate** (registered by
`AddIso3166()`); each leaf is also independently injectable:

```csharp
public sealed class Geo(IIso3166 iso, ICountriesDataset countries)
{
    public string Hawaii => iso.CountrySubdivisions["US-HI"].Name;         // "Hawaii"
    public string Romania => countries["RO"].GetLocalizedName(new CultureInfo("ro")); // "România"
}
```

### Lookup contract (family-wide)

Every dataset follows the same shape, matching BCL dictionary conventions:

- `this[key]` — returns the value or **throws** `KeyNotFoundException` (use for known-constant keys).
- `Find(key)` — returns the value or `null`.
- `TryGet(key, out value)` — returns `bool`.

## Countries

Country identity (codes and the English name) is sourced from the bundled ISO 3166-1 dataset (namespace
`ES.FX.OpenData.Countries.ISO3166`) and layered with localized names — there is no duplicate country list. The
dataset serves exactly the ISO 3166-1 alpha-2 set.

```csharp
ICountriesDataset countries = /* injected */;

countries["RO"].Name;              // "Romania" (English short name)
countries.FindByNumericCode(642);  // Romania
countries["RO"].GetLocalizedName(new CultureInfo("ro"));  // "România"
countries.Find("XK");              // null — XK is not an ISO 3166-1 alpha-2 code
```

`GetLocalizedName(CultureInfo)` walks the culture's parent chain and falls back to the English `Name`. The
package guarantees `en` and `ro`.

**Generated code constants.** The package generates four constant classes at build time from the ISO 3166-1
data. Three are keyed by country name (`CountryAlpha2Codes`, `CountryAlpha3Codes`, `CountryNumericCodes`); a
fourth, `CountryCodes`, is keyed by the alpha-2 code itself — the member name *is* the code and its XML doc
names the country, a strongly-typed alternative to magic-string codes:

```csharp
countries[CountryAlpha2Codes.Romania].Name;   // "Romania"  (CountryAlpha2Codes.Romania == "RO")
CountryAlpha3Codes.Romania;                    // "ROU"
CountryNumericCodes.Romania;                   // 642
CountryCodes.RO;                               // "RO"      (code-keyed: CountryCodes.RO == "RO")
```

**Subdivisions (regions/states).** The same package ships a *separate* dataset — `ICountrySubdivisionsDataset`,
registered with `AddCountrySubdivisions()` — serving each country's ISO 3166-2 subdivisions as curated
`CountrySubdivision` objects, the subdivision counterpart to `Country` (same localized-name shape,
`GetLocalizedName(culture)`, `en` guaranteed). Inject it alongside `ICountriesDataset` for a cascading
country → region picker:

```csharp
services.AddCountrySubdivisions();   // just this dataset — or AddCountries() for the whole library

public sealed class RegionPicker(ICountrySubdivisionsDataset subdivisions)
{
    // Regions of the selected country, top-level only (ForCountry includes nested subdivisions).
    public IEnumerable<CountrySubdivision> Regions(string countryAlpha2) =>
        subdivisions.ForCountry(countryAlpha2).Where(s => s.Parent is null);
}

subdivisions["US-HI"];       // indexer — throws KeyNotFoundException for an unknown code
subdivisions.Find("US-HI");  // nullable round-trip of a persisted code
```

`ForCountry(alpha2)` never throws — it returns an empty list for an unknown or subdivision-less country — and
includes nested subdivisions, so filter on `Parent is null` for a flat list. `AddCountries()` registers the
whole library (subdivisions included); reach for the granular `AddCountrySubdivisions()` / `AddIso3166*()`
methods when you want just one dataset and pay only for what you register. Localized subdivision names ship
only where a curated translation exists (`en` is always present, equal to the ISO name); other cultures fall
back to `Name`.

## Currencies

`ES.FX.OpenData.Currencies` follows the same two-layer shape as Countries, sourced from **ISO 4217**:
`ICurrenciesDataset` is the curated dataset (currencies with localized names), built on the raw
`IIso4217Currencies` (namespace `ES.FX.OpenData.Currencies.ISO4217`). `AddCurrencies()` registers both;
`AddIso4217()` registers just the raw dataset. Currency identity (alpha-3, numeric code, English name) comes
from the ISO 4217 dataset — `en` is always present, other cultures fall back to it until a curated translation
ships.

```csharp
services.AddCurrencies();

public sealed class Money(ICurrenciesDataset currencies)
{
    public Currency Ron => currencies[CurrencyAlpha3Codes.RomanianLeu];   // "RON", numeric 946
    public Currency? ByCode(string alpha3) => currencies.Find(alpha3);     // nullable round-trip
    public Currency? ByNumeric(int numeric) => currencies.FindByNumericCode(numeric); // 978 → EUR
}
```

Generated `CurrencyAlpha3Codes` / `CurrencyNumericCodes` are keyed by currency name (e.g. `CurrencyAlpha3Codes.Euro`
is `"EUR"`); a third class, `CurrencyCodes`, is keyed by the alpha-3 code itself (`CurrencyCodes.RON == "RON"`, its
XML doc naming the currency). All give readable, compile-time-checked codes.

## Romania — SIRUTA territorial units

The raw SIRUTA `DENLOC` column is ALL CAPS with legacy cedilla diacritics. This dataset serves **clean
names**: `Name`/`DisplayName` are title-cased with modern comma-below diacritics (`Bărăști`, `Timișoara`), and
villages belonging to a commune get a disambiguating `DisplayName` (`"Bărăști (Albac)"`). Each unit also exposes
two normalized forms: `SearchNormalizedName` — the folded search key `Search` matches against (diacritics
stripped, hyphens spaced, lower-cased) — and `DisplayNormalizedName` — a diacritic-free display form
(title-cased, hyphens kept) for ASCII-only display or interop.

```csharp
IRomanianTerritorialUnitsDataset ro = /* injected */;

ro[1026].DisplayName;              // "Alba Iulia"
ro.Find(10);                       // Județul Alba — resolves at ANY level, never throws
ro.GetLocalitiesInCounty("CJ");    // localities of Cluj county (also accepts "RO-CJ")
ro.GetUatsInCounty("CJ");          // UAT-level units (municipalities, towns, communes) of Cluj
ro.GetChildren(10);                // direct children of a unit (a county's UATs, a UAT's localities)
ro.GetCounty(ro[1026]);            // the enriched RomanianCounty a unit belongs to
ro.Counties;                       // 42 first-level subdivisions (41 counties + Bucharest)
ro.Search("cluj-napoca");          // localities, diacritic-, case-, and hyphen-insensitive
```

The 42 first-level subdivisions (41 counties/județe plus the Municipality of Bucharest) are the ISO 3166-2
`RO-*` subdivisions themselves: their code, name, and the plate/"cod auto" abbreviation come straight from
the bundled ISO 3166-2 dataset (`ES.FX.OpenData.Countries.ISO3166`), enriched with a curated side-table
(county seat, SIRUTA code).
`Search` folds the query the same way stored names are folded, so `"cluj-napoca"`, `"cluj napoca"`,
`"CLUJ-NAPOCA"`, `"timiș"` and `"timis"` all match, and results are materialized and deterministically ordered.
Hierarchy navigation is pre-indexed — `GetChildren`, `GetUatsInCounty`, `GetParent`, and `GetCounty` need no
manual filtering, so a cascading county→UAT→locality picker is direct.

## Clients (VIES, ANAF)

Clients are injected directly (`IViesClient` / `IAnafVatCheckClient`). Each is a **singleton over
`IHttpClientFactory`** (safe to inject into consumers of any lifetime). No resilience handler is applied by
default — chain one via the `configureHttpClient` escape hatch, or rely on your host's defaults.

```csharp
services.AddVies();
services.AddRomaniaAnaf(o => o.RequestsPerSecond = 1);   // ANAF throttles per source IP
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

Each structured address on a company result (registered office and fiscal domicile) is **linked to the SIRUTA
register**. ANAF returns opaque location codes; the client maps `(cod_Judet, cod_Localitate)` through an
embedded ANAF→SIRUTA crosswalk and looks the code up in `ES.FX.OpenData.Romania.TerritorialUnits`, exposing
`RomanianLocality` (the exact locality), `RomanianUat` (its municipality/town/commune), and `RomanianCounty`. Any of
these is `null` when the code is blank or absent from the shipped dataset edition — the county still resolves
from the plate code even when the locality can't.

```csharp
var office = company!.RegisteredOfficeAddress!;
var siruta = office.RomanianLocality?.SirutaCode;      // e.g. 54984 (Cluj-Napoca)
var uat    = office.RomanianUat?.Name;             // e.g. "Municipiul Cluj-Napoca"
var county = office.RomanianCounty?.IsoCode;       // e.g. "RO-CJ"
```

Both clients validate their options at host startup (`ValidateOnStart`) and emit an OpenTelemetry span per
operation on an `ActivitySource` named after the package — add `"ES.FX.OpenData.Vies"` /
`"ES.FX.OpenData.Romania.Anaf.VatCheck"` to your tracer to capture them.

> [!NOTE]
> These clients are plain libraries with no Ignite Spark — there is nothing to configure beyond a base URL and
> (for ANAF) the request budget.

## Editions & provenance

Datasets with a dated release ship a specific, documented **edition** of their data (SIRUTA `2025-12`; the
ANAF→SIRUTA crosswalk `2026-07`). The data is embedded and edition-stamped at the package level; a change of
edition is always at least a minor package-version bump (never a patch), so consumers that persist codes can
gate on the package version. Editions are not exposed as a runtime API. The ISO 3166 and ISO 4217 datasets (and
the curated Countries / Currencies built on them) simply track the published ISO code lists.

- **SIRUTA** — published by INS (Institutul Național de Statistică). Current edition: `2025-12`. The data is
  canonicalized at ingest (NFC, legacy cedilla → comma-below) and embedded gzip-compressed (~260 KB).
- **ANAF→SIRUTA crosswalk** — the `(cod_Judet, cod_Localitate) → SIRUTA` map embedded in
  `ES.FX.OpenData.Romania.Anaf`, extracted verbatim from the Ministry of Finance geographic nomenclature.
  Current edition: `2026-07`. Because the crosswalk is on a later edition than the shipped SIRUTA register (INS
  `2025-12`), about 2% of its entries (353 of 17,205) point at SIRUTA codes absent from that register — mostly
  within-range recoded or merged localities, not simply newer codes — so `RomanianLocality` / `RomanianUat`
  resolve for roughly 98% of addresses and are `null` for the rest (the county still resolves from the plate
  code). The gap closes when the shipped SIRUTA edition catches up to the crosswalk.

## See also

- [Framework libraries](./index.md)
- [Creating a new ES.FX library](../development/creating-a-library.md)
- [Primitives](../development/primitives.md) — the `ToTitleCase` / `RemoveDiacritics` helpers the datasets build on.
