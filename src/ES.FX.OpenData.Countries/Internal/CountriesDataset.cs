using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ES.FX.OpenData.Countries.ISO3166;

namespace ES.FX.OpenData.Countries.Internal;

internal sealed class CountriesDataset : ICountriesDataset
{
    private const string LocalizedNamesResource = "ES.FX.OpenData.Countries.country-localized-names.json";

    private static readonly Assembly ResourceAssembly = typeof(CountriesDataset).Assembly;

    private readonly IIso3166Countries _iso3166Countries;
    private readonly Lazy<CountriesStore> _store;

    public CountriesDataset(IIso3166Countries iso3166Countries)
    {
        _iso3166Countries = iso3166Countries;
        _store = new Lazy<CountriesStore>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<Country> All => _store.Value.All;

    public Country this[string alpha2] =>
        Find(alpha2) ?? throw new KeyNotFoundException(
            $"Unknown ISO 3166-1 alpha-2 code '{alpha2}'. The Countries dataset contains {All.Count} " +
            $"entries. Use Find/TryGet for codes that may be absent.");

    public Country? Find(string alpha2)
    {
        ArgumentNullException.ThrowIfNull(alpha2);
        return _store.Value.ByAlpha2.TryGetValue(alpha2, out var country) ? country : null;
    }

    public bool TryGet(string alpha2, [NotNullWhen(true)] out Country? country)
    {
        country = Find(alpha2);
        return country is not null;
    }

    public Country? FindByAlpha3(string alpha3)
    {
        ArgumentNullException.ThrowIfNull(alpha3);
        return _store.Value.ByAlpha3.TryGetValue(alpha3, out var country) ? country : null;
    }

    public Country? FindByNumericCode(int numericCode) =>
        _store.Value.ByNumeric.TryGetValue(numericCode, out var country) ? country : null;

    private CountriesStore Load()
    {
        var localizedNames = OpenDataResources.DeserializeJson(ResourceAssembly,
            LocalizedNamesResource, CountriesJsonContext.Default.LocalizedNamesOverlay);
        return CountriesStore.Build(_iso3166Countries.All, localizedNames);
    }
}