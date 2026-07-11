using System.Collections.Frozen;
using System.Collections.ObjectModel;
using ES.FX.OpenData.Currencies.ISO4217;

namespace ES.FX.OpenData.Currencies.Internal;

internal sealed class CurrenciesStore
{
    private CurrenciesStore(
        IReadOnlyList<Currency> all,
        FrozenDictionary<string, Currency> byAlpha3,
        FrozenDictionary<int, Currency> byNumeric)
    {
        All = all;
        ByAlpha3 = byAlpha3;
        ByNumeric = byNumeric;
    }

    public IReadOnlyList<Currency> All { get; }
    public FrozenDictionary<string, Currency> ByAlpha3 { get; }
    public FrozenDictionary<int, Currency> ByNumeric { get; }

    /// <summary>
    ///     Builds the curated currency list from the ISO 4217 dataset (single source of identity: codes and the
    ///     English name) layered with the localized-names overlay. English (<c>en</c>) is always present, equal to
    ///     the ISO 4217 name.
    /// </summary>
    public static CurrenciesStore Build(
        IReadOnlyList<Iso4217Currency> currencies,
        IReadOnlyDictionary<string, Dictionary<string, string>> localizedNames)
    {
        var all = new Currency[currencies.Count];
        for (var i = 0; i < currencies.Count; i++)
        {
            var source = currencies[i];

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["en"] = source.Name };
            if (localizedNames.TryGetValue(source.Alpha3, out var overlay))
                foreach (var (culture, localized) in overlay)
                    names[culture] = localized;

            all[i] = new Currency
            {
                Alpha3 = source.Alpha3,
                NumericCode = source.NumericCode,
                Name = source.Name,
                LocalizedNames = new ReadOnlyDictionary<string, string>(names)
            };
        }

        var byAlpha3 = all.ToFrozenDictionary(c => c.Alpha3, StringComparer.OrdinalIgnoreCase);
        var byNumeric = all.ToFrozenDictionary(c => c.NumericCode);
        return new CurrenciesStore(Array.AsReadOnly(all), byAlpha3, byNumeric);
    }
}