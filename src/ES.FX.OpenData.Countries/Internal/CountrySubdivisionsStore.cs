using System.Collections.Frozen;
using System.Collections.ObjectModel;
using ES.FX.OpenData.Countries.ISO3166;

namespace ES.FX.OpenData.Countries.Internal;

internal sealed class CountrySubdivisionsStore
{
    private CountrySubdivisionsStore(
        IReadOnlyList<CountrySubdivision> all,
        FrozenDictionary<string, CountrySubdivision> byCode,
        FrozenDictionary<string, IReadOnlyList<CountrySubdivision>> byCountry)
    {
        All = all;
        ByCode = byCode;
        ByCountry = byCountry;
    }

    public IReadOnlyList<CountrySubdivision> All { get; }
    public FrozenDictionary<string, CountrySubdivision> ByCode { get; }
    public FrozenDictionary<string, IReadOnlyList<CountrySubdivision>> ByCountry { get; }

    /// <summary>
    ///     Builds the curated subdivision list from the ISO 3166-2 dataset (single source of identity: code,
    ///     country prefix, ISO name, type, and parent) layered with the localized-names overlay. The English
    ///     display name (<c>en</c>) is the ISO-recorded name; other cultures come from the overlay when present.
    ///     Note: while the overlay ships empty this curated layer still fully re-materializes every raw
    ///     subdivision (plus a parallel ByCode/ByCountry index) on top of the raw ISO 3166-2 store it derives from.
    /// </summary>
    public static CountrySubdivisionsStore Build(
        IReadOnlyList<Iso3166CountrySubdivision> subdivisions,
        IReadOnlyDictionary<string, Dictionary<string, string>> localizedNames)
    {
        var all = new CountrySubdivision[subdivisions.Count];
        for (var i = 0; i < subdivisions.Count; i++)
        {
            var source = subdivisions[i];

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = source.Name };
            if (localizedNames.TryGetValue(source.Code, out var overlay))
                foreach (var (culture, localized) in overlay)
                    names[culture] = localized;

            all[i] = new CountrySubdivision
            {
                Code = source.Code,
                CountryAlpha2 = source.CountryAlpha2,
                Name = source.Name,
                Type = source.Type,
                Parent = source.Parent,
                LocalizedNames = new ReadOnlyDictionary<string, string>(names)
            };
        }

        var byCode = all.ToFrozenDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        var byCountry = all
            .GroupBy(s => s.CountryAlpha2, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(
                g => g.Key,
                g => (IReadOnlyList<CountrySubdivision>)Array.AsReadOnly(g.ToArray()),
                StringComparer.OrdinalIgnoreCase);
        return new CountrySubdivisionsStore(Array.AsReadOnly(all), byCode, byCountry);
    }
}