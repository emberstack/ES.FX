# Countries dataset

Localized-names overlay for the `ES.FX.OpenData.Countries` package.

## Single source of identity

Country **identity** — alpha-2, alpha-3, numeric codes, and the English name — is **not** duplicated here.
It comes from the bundled ISO 3166-1 dataset (namespace `ES.FX.OpenData.Countries.ISO3166`).
`ES.FX.OpenData.Countries` layers two things on top of that single source:

1. **A friendlier English display name:** it prefers the ISO `common_name` over the formal `name`
   (e.g. `South Korea` instead of `Korea, Republic of`, `Bolivia` instead of `Bolivia, Plurinational State of`).
2. **Localized names**, from the file below.

## Files

| File | Contents |
| --- | --- |
| `country-localized-names.json` | Map of ISO 3166-1 alpha-2 code → (culture → localized name), e.g. `{ "RO": { "ro": "România" } }`. English is derived from identity and is **not** stored here. |

This file is embedded (by link) into `ES.FX.OpenData.Countries` as the assembly's data resource.

Romanian (`ro`) is complete (249/249); cultures with no shipped name fall back to the English name at runtime
(`Country.GetLocalizedName`).

> The dataset ships only canonical ISO 3166-1 entries. Non-standard or partner-specific codes belong in a
> consumer overlay, not the shared dataset.
