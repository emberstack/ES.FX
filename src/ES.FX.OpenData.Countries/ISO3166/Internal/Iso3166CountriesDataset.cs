using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ES.FX.OpenData.Countries.ISO3166.Internal;

internal sealed class Iso3166CountriesDataset : IIso3166Countries
{
    private const string Resource = "ES.FX.OpenData.Countries.ISO3166.iso_3166-1.json";
    private static readonly Assembly ResourceAssembly = typeof(Iso3166CountriesDataset).Assembly;

    private readonly Lazy<Iso3166CountriesStore> _store = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<Iso3166Country> All => _store.Value.All;

    public Iso3166Country this[string alpha2] =>
        Find(alpha2) ?? throw new KeyNotFoundException(
            $"Unknown ISO 3166-1 alpha-2 code '{alpha2}'. The dataset contains " +
            $"{All.Count} countries. Use Find/TryGet for codes that may be absent.");

    public Iso3166Country? Find(string alpha2)
    {
        ArgumentNullException.ThrowIfNull(alpha2);
        return _store.Value.ByAlpha2.TryGetValue(alpha2, out var country) ? country : null;
    }

    public bool TryGet(string alpha2, [NotNullWhen(true)] out Iso3166Country? country)
    {
        country = Find(alpha2);
        return country is not null;
    }

    public Iso3166Country? FindByAlpha3(string alpha3)
    {
        ArgumentNullException.ThrowIfNull(alpha3);
        return _store.Value.ByAlpha3.TryGetValue(alpha3, out var country) ? country : null;
    }

    public Iso3166Country? FindByNumericCode(int numericCode) =>
        _store.Value.ByNumeric.TryGetValue(numericCode, out var country) ? country : null;

    private static Iso3166CountriesStore Load()
    {
        var document = OpenDataResources.DeserializeJson(ResourceAssembly, Resource,
            Iso3166JsonContext.Default.Iso3166Part1Document);
        return Iso3166CountriesStore.Build(document.Entries);
    }
}