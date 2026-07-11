using System.Collections.Frozen;
using System.Globalization;

namespace ES.FX.OpenData.Currencies.ISO4217.Internal;

internal sealed class Iso4217CurrenciesStore
{
    private Iso4217CurrenciesStore(
        IReadOnlyList<Iso4217Currency> all,
        FrozenDictionary<string, Iso4217Currency> byAlpha3,
        FrozenDictionary<int, Iso4217Currency> byNumeric)
    {
        All = all;
        ByAlpha3 = byAlpha3;
        ByNumeric = byNumeric;
    }

    public IReadOnlyList<Iso4217Currency> All { get; }
    public FrozenDictionary<string, Iso4217Currency> ByAlpha3 { get; }
    public FrozenDictionary<int, Iso4217Currency> ByNumeric { get; }

    public static Iso4217CurrenciesStore Build(IReadOnlyList<Iso4217CurrencyRow> rows)
    {
        var all = new Iso4217Currency[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            all[i] = new Iso4217Currency
            {
                Alpha3 = row.Alpha3,
                NumericCode = int.Parse(row.Numeric, CultureInfo.InvariantCulture),
                Name = row.Name
            };
        }

        var byAlpha3 = all.ToFrozenDictionary(c => c.Alpha3, StringComparer.OrdinalIgnoreCase);
        var byNumeric = all.ToFrozenDictionary(c => c.NumericCode);
        return new Iso4217CurrenciesStore(Array.AsReadOnly(all), byAlpha3, byNumeric);
    }
}