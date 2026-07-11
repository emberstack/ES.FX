using System.Collections.Frozen;
using System.Globalization;

namespace ES.FX.OpenData.Countries.ISO3166.Internal;

internal sealed class Iso3166CountriesStore
{
    private Iso3166CountriesStore(
        IReadOnlyList<Iso3166Country> all,
        FrozenDictionary<string, Iso3166Country> byAlpha2,
        FrozenDictionary<string, Iso3166Country> byAlpha3,
        FrozenDictionary<int, Iso3166Country> byNumeric)
    {
        All = all;
        ByAlpha2 = byAlpha2;
        ByAlpha3 = byAlpha3;
        ByNumeric = byNumeric;
    }

    public IReadOnlyList<Iso3166Country> All { get; }
    public FrozenDictionary<string, Iso3166Country> ByAlpha2 { get; }
    public FrozenDictionary<string, Iso3166Country> ByAlpha3 { get; }
    public FrozenDictionary<int, Iso3166Country> ByNumeric { get; }

    public static Iso3166CountriesStore Build(IReadOnlyList<Iso3166CountryRow> rows)
    {
        var all = new Iso3166Country[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            all[i] = new Iso3166Country
            {
                Alpha2 = row.Alpha2,
                Alpha3 = row.Alpha3,
                NumericCode = int.Parse(row.Numeric, CultureInfo.InvariantCulture),
                Name = row.Name,
                OfficialName = row.OfficialName,
                CommonName = row.CommonName,
                Flag = row.Flag
            };
        }

        var byAlpha2 = all.ToFrozenDictionary(c => c.Alpha2, StringComparer.OrdinalIgnoreCase);
        var byAlpha3 = all.ToFrozenDictionary(c => c.Alpha3, StringComparer.OrdinalIgnoreCase);
        var byNumeric = all.ToFrozenDictionary(c => c.NumericCode);
        return new Iso3166CountriesStore(Array.AsReadOnly(all), byAlpha2, byAlpha3, byNumeric);
    }
}