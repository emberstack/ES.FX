using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ES.FX.OpenData.Countries.ISO3166;

namespace ES.FX.OpenData.Countries.Internal;

internal sealed class CountrySubdivisionsDataset : ICountrySubdivisionsDataset
{
    private const string LocalizedNamesResource = "ES.FX.OpenData.Countries.subdivision-localized-names.json";

    private static readonly Assembly ResourceAssembly = typeof(CountrySubdivisionsDataset).Assembly;

    private readonly IIso3166CountrySubdivisions _iso3166Subdivisions;
    private readonly Lazy<CountrySubdivisionsStore> _store;

    public CountrySubdivisionsDataset(IIso3166CountrySubdivisions iso3166Subdivisions)
    {
        _iso3166Subdivisions = iso3166Subdivisions;
        _store = new Lazy<CountrySubdivisionsStore>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<CountrySubdivision> All => _store.Value.All;

    public CountrySubdivision this[string code] =>
        Find(code) ?? throw new KeyNotFoundException(
            $"Unknown ISO 3166-2 subdivision code '{code}'. The dataset contains {All.Count} subdivisions. " +
            "Use Find/TryGet for codes that may be absent.");

    public CountrySubdivision? Find(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _store.Value.ByCode.TryGetValue(code, out var subdivision) ? subdivision : null;
    }

    public bool TryGet(string code, [NotNullWhen(true)] out CountrySubdivision? subdivision)
    {
        subdivision = Find(code);
        return subdivision is not null;
    }

    public IReadOnlyList<CountrySubdivision> ForCountry(string countryAlpha2)
    {
        ArgumentNullException.ThrowIfNull(countryAlpha2);
        return _store.Value.ByCountry.TryGetValue(countryAlpha2, out var subdivisions) ? subdivisions : [];
    }

    private CountrySubdivisionsStore Load()
    {
        var localizedNames = OpenDataResources.DeserializeJson(ResourceAssembly,
            LocalizedNamesResource, CountriesJsonContext.Default.LocalizedNamesOverlay);
        return CountrySubdivisionsStore.Build(_iso3166Subdivisions.All, localizedNames);
    }
}