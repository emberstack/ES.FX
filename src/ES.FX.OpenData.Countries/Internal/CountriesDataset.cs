using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ES.FX.OpenData.Countries.Internal;

internal sealed class CountriesDataset : ICountriesDataset
{
    internal static readonly OpenDatasetInfo DatasetInfo = new()
    {
        Name = "Countries",
        Edition = "2025",
        Source = "ISO 3166-1 (curated); country/area set cross-checked against UN M49",
        License = "ES.FX.OpenData code under MIT; data derived from the public ISO 3166-1 country code list.",
        Standard = "ISO 3166-1"
    };

    private const string CountriesResource = "ES.FX.OpenData.Countries.countries.json";
    private const string AliasesResource = "ES.FX.OpenData.Countries.country-aliases.json";

    private static readonly Assembly ResourceAssembly = typeof(CountriesDataset).Assembly;

    private readonly Lazy<CountriesStore> _store = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public OpenDatasetInfo Info => DatasetInfo;

    public void EnsureLoaded() => _ = _store.Value;

    public IReadOnlyList<Country> All => _store.Value.All;

    public IReadOnlyDictionary<string, Country> LookupMap => _store.Value.LookupMap;

    public Country this[string alpha2Code] =>
        Find(alpha2Code) ?? throw new KeyNotFoundException(
            $"Unknown ISO 3166-1 alpha-2 code '{alpha2Code}'. The Countries dataset (edition {DatasetInfo.Edition}) " +
            $"contains {All.Count} entries. Use Find/TryGet for codes that may be absent, or Resolve for alias codes.");

    public Country? Find(string alpha2Code)
    {
        ArgumentNullException.ThrowIfNull(alpha2Code);
        return _store.Value.ByAlpha2.TryGetValue(alpha2Code, out var country) ? country : null;
    }

    public bool TryGet(string alpha2Code, [NotNullWhen(true)] out Country? country)
    {
        country = Find(alpha2Code);
        return country is not null;
    }

    public Country? FindByNumericCode(int numericCode) =>
        _store.Value.ByNumeric.TryGetValue(numericCode, out var country) ? country : null;

    public Country? Resolve(string anyKnownCode)
    {
        ArgumentNullException.ThrowIfNull(anyKnownCode);
        return _store.Value.LookupMap.TryGetValue(anyKnownCode, out var country) ? country : null;
    }

    private static CountriesStore Load()
    {
        var rows = OpenDataResources.DeserializeJson(ResourceAssembly,
            CountriesResource, CountriesJsonContext.Default.CountryRowArray);
        var aliases = OpenDataResources.DeserializeJson(ResourceAssembly,
            AliasesResource, CountriesJsonContext.Default.CountryAliasRowArray);
        return CountriesStore.Build(rows, aliases);
    }
}
