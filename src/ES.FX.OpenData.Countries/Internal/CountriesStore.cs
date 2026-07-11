using System.Collections.Frozen;
using System.Collections.ObjectModel;
using ES.FX.OpenData.Countries.ISO3166;

namespace ES.FX.OpenData.Countries.Internal;

internal sealed class CountriesStore
{
    private CountriesStore(
        IReadOnlyList<Country> all,
        FrozenDictionary<string, Country> byAlpha2,
        FrozenDictionary<string, Country> byAlpha3,
        FrozenDictionary<int, Country> byNumeric)
    {
        All = all;
        ByAlpha2 = byAlpha2;
        ByAlpha3 = byAlpha3;
        ByNumeric = byNumeric;
    }

    public IReadOnlyList<Country> All { get; }
    public FrozenDictionary<string, Country> ByAlpha2 { get; }
    public FrozenDictionary<string, Country> ByAlpha3 { get; }
    public FrozenDictionary<int, Country> ByNumeric { get; }

    /// <summary>
    ///     Builds the curated country list from the ISO 3166-1 dataset (single source of identity: codes and
    ///     the English name) layered with the localized-names overlay. The English display name prefers the
    ///     ISO <c>common_name</c> (e.g. "South Korea") over the formal <c>name</c> ("Korea, Republic of").
    /// </summary>
    public static CountriesStore Build(
        IReadOnlyList<Iso3166Country> countries,
        IReadOnlyDictionary<string, Dictionary<string, string>> localizedNames)
    {
        var all = new Country[countries.Count];
        for (var i = 0; i < countries.Count; i++)
        {
            var source = countries[i];
            var name = source.CommonName ?? source.Name;

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = name };
            if (localizedNames.TryGetValue(source.Alpha2, out var overlay))
                foreach (var (culture, localized) in overlay)
                    names[culture] = localized;

            all[i] = new Country
            {
                Alpha2 = source.Alpha2,
                Alpha3 = source.Alpha3,
                NumericCode = source.NumericCode,
                Name = name,
                LocalizedNames = new ReadOnlyDictionary<string, string>(names)
            };
        }

        var byAlpha2 = all.ToFrozenDictionary(c => c.Alpha2, StringComparer.OrdinalIgnoreCase);
        var byAlpha3 = all.ToFrozenDictionary(c => c.Alpha3, StringComparer.OrdinalIgnoreCase);
        var byNumeric = all.ToFrozenDictionary(c => c.NumericCode);
        return new CountriesStore(Array.AsReadOnly(all), byAlpha2, byAlpha3, byNumeric);
    }
}