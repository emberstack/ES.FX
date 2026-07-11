using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ES.FX.OpenData.Currencies.ISO4217.Internal;

internal sealed class Iso4217CurrenciesDataset : IIso4217Currencies
{
    private const string Resource = "ES.FX.OpenData.Currencies.ISO4217.iso_4217.json";
    private static readonly Assembly ResourceAssembly = typeof(Iso4217CurrenciesDataset).Assembly;

    private readonly Lazy<Iso4217CurrenciesStore> _store = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<Iso4217Currency> All => _store.Value.All;

    public Iso4217Currency this[string alpha3] =>
        Find(alpha3) ?? throw new KeyNotFoundException(
            $"Unknown ISO 4217 alpha-3 code '{alpha3}'. The dataset contains " +
            $"{All.Count} currencies. Use Find/TryGet for codes that may be absent.");

    public Iso4217Currency? Find(string alpha3)
    {
        ArgumentNullException.ThrowIfNull(alpha3);
        return _store.Value.ByAlpha3.TryGetValue(alpha3, out var currency) ? currency : null;
    }

    public bool TryGet(string alpha3, [NotNullWhen(true)] out Iso4217Currency? currency)
    {
        currency = Find(alpha3);
        return currency is not null;
    }

    public Iso4217Currency? FindByNumericCode(int numericCode) =>
        _store.Value.ByNumeric.TryGetValue(numericCode, out var currency) ? currency : null;

    private static Iso4217CurrenciesStore Load()
    {
        var document = OpenDataResources.DeserializeJson(ResourceAssembly, Resource,
            Iso4217JsonContext.Default.Iso4217Document);
        return Iso4217CurrenciesStore.Build(document.Entries);
    }
}