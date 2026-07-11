# ISO 3166 dataset

The ISO 3166 code list bundled in `ES.FX.OpenData.Countries` (namespace `ES.FX.OpenData.Countries.ISO3166`) —
the three parts of the standard: country codes, subdivision codes, and formerly used country codes.

## Files

| File | Standard | Contents |
| --- | --- | --- |
| `iso_3166-1.json` | ISO 3166-1 | 249 countries: `alpha_2`, `alpha_3`, `numeric`, `name`, `official_name?`, `common_name?`, `flag`. |
| `iso_3166-2.json` | ISO 3166-2 | 5,046 subdivisions: `code` (e.g. `US-HI`), `name`, `type`, `parent?`. |
| `iso_3166-3.json` | ISO 3166-3 | 31 formerly used country codes: `alpha_4` (e.g. `ANHH`), `alpha_3`, `alpha_2?`, `numeric?`, `name`, `comment?`, `withdrawal_date?`. |

Each file is JSON (snake_case keys, numeric codes as zero-padded strings) and is embedded (by link) into
`ES.FX.OpenData.Countries` as an assembly resource (LogicalName `ES.FX.OpenData.Countries.ISO3166.iso_3166-*.json`).
