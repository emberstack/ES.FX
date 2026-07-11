using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ES.FX.OpenData.Currencies.ISO4217;

namespace ES.FX.OpenData.Currencies.Internal;

internal sealed class CurrenciesDataset : ICurrenciesDataset
{
    private const string LocalizedNamesResource = "ES.FX.OpenData.Currencies.currency-localized-names.json";

    private static readonly Assembly ResourceAssembly = typeof(CurrenciesDataset).Assembly;

    private readonly IIso4217Currencies _iso4217Currencies;
    private readonly Lazy<CurrenciesStore> _store;

    public CurrenciesDataset(IIso4217Currencies iso4217Currencies)
    {
        _iso4217Currencies = iso4217Currencies;
        _store = new Lazy<CurrenciesStore>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<Currency> All => _store.Value.All;

    public Currency this[string alpha3] =>
        Find(alpha3) ?? throw new KeyNotFoundException(
            $"Unknown ISO 4217 alpha-3 code '{alpha3}'. The Currencies dataset contains {All.Count} " +
            $"entries. Use Find/TryGet for codes that may be absent.");

    public Currency? Find(string alpha3)
    {
        ArgumentNullException.ThrowIfNull(alpha3);
        return _store.Value.ByAlpha3.TryGetValue(alpha3, out var currency) ? currency : null;
    }

    public bool TryGet(string alpha3, [NotNullWhen(true)] out Currency? currency)
    {
        currency = Find(alpha3);
        return currency is not null;
    }

    public Currency? FindByNumericCode(int numericCode) =>
        _store.Value.ByNumeric.TryGetValue(numericCode, out var currency) ? currency : null;

    private CurrenciesStore Load()
    {
        var localizedNames = OpenDataResources.DeserializeJson(ResourceAssembly,
            LocalizedNamesResource, CurrenciesJsonContext.Default.LocalizedNamesOverlay);
        return CurrenciesStore.Build(_iso4217Currencies.All, localizedNames);
    }
}