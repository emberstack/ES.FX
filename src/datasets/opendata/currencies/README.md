# Currencies localized-names overlay

`currency-localized-names.json` maps an ISO 4217 alpha-3 code to a `{ culture -> localized name }` object,
layered on top of the bundled ISO 4217 dataset by `ES.FX.OpenData.Currencies`:

```json
{ "USD": { "ro": "Dolar american" }, "RON": { "ro": "Leu românesc" } }
```

Currency **identity** — alpha-3, numeric code, and the English name — is **not** duplicated here; it comes
from the bundled ISO 4217 dataset (namespace `ES.FX.OpenData.Currencies.ISO4217`). This overlay only adds
localized display names. English (`en`) is always present at runtime, equal to the ISO 4217 name — it is not
stored here.

The overlay currently ships **empty** (`{}`): structure is in place, curated translations can be filled in
later (e.g. Romanian currency names). Until then `Currency.GetLocalizedName` returns the English ISO name for
any culture.
