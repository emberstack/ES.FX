using System.Collections.Frozen;

namespace ES.FX.OpenData.Countries.ISO3166.Internal;

internal sealed class Iso3166CountrySubdivisionsStore
{
    private Iso3166CountrySubdivisionsStore(
        IReadOnlyList<Iso3166CountrySubdivision> all,
        FrozenDictionary<string, Iso3166CountrySubdivision> byCode,
        FrozenDictionary<string, IReadOnlyList<Iso3166CountrySubdivision>> byCountry)
    {
        All = all;
        ByCode = byCode;
        ByCountry = byCountry;
    }

    public IReadOnlyList<Iso3166CountrySubdivision> All { get; }
    public FrozenDictionary<string, Iso3166CountrySubdivision> ByCode { get; }
    public FrozenDictionary<string, IReadOnlyList<Iso3166CountrySubdivision>> ByCountry { get; }

    public static Iso3166CountrySubdivisionsStore Build(IReadOnlyList<Iso3166CountrySubdivisionRow> rows)
    {
        var all = new Iso3166CountrySubdivision[rows.Count];
        var byCountry = new Dictionary<string, List<Iso3166CountrySubdivision>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var hyphen = row.Code.IndexOf('-');
            var countryAlpha2 = hyphen > 0 ? row.Code[..hyphen] : row.Code;
            var subdivision = new Iso3166CountrySubdivision
            {
                Code = row.Code,
                CountryAlpha2 = countryAlpha2,
                Name = row.Name,
                Type = row.Type,
                Parent = row.Parent
            };
            all[i] = subdivision;

            if (!byCountry.TryGetValue(countryAlpha2, out var list))
                byCountry[countryAlpha2] = list = [];
            list.Add(subdivision);
        }

        var byCode = all.ToFrozenDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        // Wrap each group in a ReadOnlyCollection so a caller can't downcast ForCountry's result back to the
        // live List<T> and resize the shared, singleton-cached index.
        var grouped = byCountry.ToFrozenDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<Iso3166CountrySubdivision>)kvp.Value.AsReadOnly(),
            StringComparer.OrdinalIgnoreCase);
        return new Iso3166CountrySubdivisionsStore(Array.AsReadOnly(all), byCode, grouped);
    }
}