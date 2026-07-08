using System.Collections.Frozen;

namespace ES.FX.OpenData.Countries.Internal;

internal sealed class CountriesStore
{
    private CountriesStore(
        IReadOnlyList<Country> all,
        FrozenDictionary<string, Country> byAlpha2,
        FrozenDictionary<int, Country> byNumeric,
        FrozenDictionary<string, Country> lookupMap)
    {
        All = all;
        ByAlpha2 = byAlpha2;
        ByNumeric = byNumeric;
        LookupMap = lookupMap;
    }

    public IReadOnlyList<Country> All { get; }
    public FrozenDictionary<string, Country> ByAlpha2 { get; }
    public FrozenDictionary<int, Country> ByNumeric { get; }
    public FrozenDictionary<string, Country> LookupMap { get; }

    public static CountriesStore Build(IReadOnlyList<CountryRow> rows, IReadOnlyList<CountryAliasRow> aliases)
    {
        var all = new Country[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            all[i] = new Country
            {
                Alpha2 = row.Alpha2,
                Alpha3 = row.Alpha3,
                NumericCode = row.NumericCode,
                Name = row.Name,
                LocalizedNames = LocalizedNames(row.Name, row.NameRo)
            };
        }

        var byAlpha2 = all.ToFrozenDictionary(c => c.Alpha2, StringComparer.OrdinalIgnoreCase);
        var byNumeric = all.ToFrozenDictionary(c => c.NumericCode);

        // The lookup superset: canonical alpha-2 keys plus non-standard alias codes. Alias entries inherit the
        // canonical country's codes but keep the territory's own display names, and live ONLY here — never in
        // All / ByAlpha2 — so the canonical list stays unpolluted.
        var lookup = new Dictionary<string, Country>(byAlpha2, StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases)
        {
            byAlpha2.TryGetValue(alias.Alpha2, out var parent);
            lookup[alias.Code] = new Country
            {
                Alpha2 = parent?.Alpha2 ?? alias.Alpha2,
                Alpha3 = parent?.Alpha3 ?? alias.Alpha3,
                NumericCode = parent?.NumericCode ?? 0,
                Name = alias.Name,
                LocalizedNames = LocalizedNames(alias.Name, alias.NameRo)
            };
        }

        return new CountriesStore(all, byAlpha2, byNumeric,
            lookup.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, string> LocalizedNames(string english, string romanian)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = english };
        if (!string.IsNullOrEmpty(romanian)) map["ro"] = romanian;
        return map;
    }
}
