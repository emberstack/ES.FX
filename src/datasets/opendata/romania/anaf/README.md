# ANAF → SIRUTA crosswalk

`anaf-siruta.2026-07.json` is a **slim** map from an ANAF address's location codes to a SIRUTA code:

```json
{ "<cod_Judet>": { "<cod_Localitate>": <sirutaCode> } }
```

It is embedded in **`ES.FX.OpenData.Romania.Anaf`** and used to resolve each company address to its SIRUTA
locality / UAT / county (see `AnafSirutaCrosswalk` and `AnafVatCheckAddress.RomanianLocality`/`RomanianUat`/`RomanianCounty`).

## Source

The Romanian Ministry of Finance publishes the geographic nomenclature at
<https://mfinante.gov.ro/ro/web/site/nomenclatoare-geografice-mfp> (the "localities", **not** the streets),
served as static XML under `https://mfinante.gov.ro/static/40/Mfp/nomenclatoare/`:

- `nomJudete.xml` — 42 counties: `COD` (= ANAF `cod_Judet`), `AUTO` (plate/ISO 3166-2 suffix), `DENUMIRE`.
- `nomLocalitati_{XX}_<date>.xml` — localities per county code `XX` ∈ `{1..40, 51, 52}`. Each row carries
  the composite key `(JUD_COD, COD)` — **exactly** ANAF's `(cod_Judet, cod_Localitate)` — plus **`COD_SIRUTA`**
  (the locality's SIRUTA code, taken verbatim) and `COD_SIRUTA_TATA` (parent).

`COD` and `COD_SIRUTA` differ: `COD` is the MFP/ANAF locality code, `COD_SIRUTA` is the INS SIRUTA code the
crosswalk maps to.

## Edition

Current — and frozen — edition: **`2026-07`** (MFP export dated 10.07.2026). This crosswalk is **not
regenerated**; `anaf-siruta.2026-07.json` is the committed artifact. The raw MFP XML source is **not
committed** (`ANAF_EXPORT/` is git-ignored).

The map is **ANAF truth**, taken verbatim; it is not remapped to our SIRUTA snapshot. A `COD_SIRUTA` that is
newer than the shipped `ES.FX.OpenData.Romania.TerritorialUnits` SIRUTA edition simply won't resolve until
SIRUTA is refreshed (`AnafVatCheckAddress.RomanianLocality` is `null` for those, gracefully).

## Producing a future edition

`generate-crosswalk.py` is kept for when a newer MFP export is adopted:

1. Download the nomenclature XMLs into a folder named `ANAF_EXPORT/` at the repo root (localities only — skip
   the `strazi`/street files). The MFP static host gates on a `Referer`; PowerShell `Invoke-WebRequest` with a
   browser `User-Agent` + `Referer: …/apps/publinomLoc.html` works where a bare `curl` does not.
2. Bump `EDITION` in the script and run `python generate-crosswalk.py`; it re-extracts, re-validates, and
   writes `anaf-siruta.<edition>.json`. Update the `EmbeddedResource` in `ES.FX.OpenData.Romania.Anaf.csproj`
   and the `ResourceName` const in `AnafSirutaCrosswalk`.

## Validation gates (generator fails, writing nothing, if any break)

- every source XML parses; every row has `JUD_COD` / `COD` / `COD_SIRUTA`
- every `JUD_COD` is one of the 42 county codes in `nomJudete.xml`
- `JUD_COD` / `COD` / `COD_SIRUTA` are integers
- `(JUD_COD, COD)` is globally unique (the runtime lookup key)
- round-trip: the re-parsed JSON matches the in-memory map, and `12 / 103 → 54984` (Cluj-Napoca)

Rows excluded from the slim map (logged, not failures): the 42 synthetic `"Fără domiciliu în România"`
pseudo-localities (`TPL_COD = '?'`) and any row with a missing / non-positive `COD_SIRUTA`.
