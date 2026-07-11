using System.Collections.Frozen;
using System.Globalization;

namespace ES.FX.OpenData.Countries.ISO3166.Internal;

internal sealed class Iso3166FormerCountriesStore
{
    private Iso3166FormerCountriesStore(
        IReadOnlyList<Iso3166FormerCountry> all,
        FrozenDictionary<string, Iso3166FormerCountry> byAlpha4,
        FrozenDictionary<string, Iso3166FormerCountry> byAlpha2)
    {
        All = all;
        ByAlpha4 = byAlpha4;
        ByAlpha2 = byAlpha2;
    }

    public IReadOnlyList<Iso3166FormerCountry> All { get; }
    public FrozenDictionary<string, Iso3166FormerCountry> ByAlpha4 { get; }
    public FrozenDictionary<string, Iso3166FormerCountry> ByAlpha2 { get; }

    public static Iso3166FormerCountriesStore Build(IReadOnlyList<Iso3166FormerCountryRow> rows)
    {
        var all = new Iso3166FormerCountry[rows.Count];
        // A retired alpha-2 code can, in principle, have been reused; keep the first occurrence for the lookup.
        var byAlpha2 = new Dictionary<string, Iso3166FormerCountry>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var former = new Iso3166FormerCountry
            {
                Alpha4 = row.Alpha4,
                Alpha3 = row.Alpha3,
                Alpha2 = row.Alpha2,
                NumericCode = string.IsNullOrEmpty(row.Numeric)
                    ? null
                    : int.Parse(row.Numeric, CultureInfo.InvariantCulture),
                Name = row.Name,
                Comment = row.Comment,
                WithdrawalDate = row.WithdrawalDate
            };
            all[i] = former;

            if (!string.IsNullOrEmpty(former.Alpha2))
                byAlpha2.TryAdd(former.Alpha2, former);
        }

        var byAlpha4 = all.ToFrozenDictionary(f => f.Alpha4, StringComparer.OrdinalIgnoreCase);
        return new Iso3166FormerCountriesStore(
            Array.AsReadOnly(all), byAlpha4, byAlpha2.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }
}