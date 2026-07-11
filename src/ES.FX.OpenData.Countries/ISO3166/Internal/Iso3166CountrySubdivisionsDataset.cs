using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ES.FX.OpenData.Countries.ISO3166.Internal;

internal sealed class Iso3166CountrySubdivisionsDataset : IIso3166CountrySubdivisions
{
    private const string Resource = "ES.FX.OpenData.Countries.ISO3166.iso_3166-2.json";
    private static readonly Assembly ResourceAssembly = typeof(Iso3166CountrySubdivisionsDataset).Assembly;

    private readonly Lazy<Iso3166CountrySubdivisionsStore> _store = new(Load,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<Iso3166CountrySubdivision> All => _store.Value.All;

    public Iso3166CountrySubdivision this[string code] =>
        Find(code) ?? throw new KeyNotFoundException(
            $"Unknown ISO 3166-2 subdivision code '{code}'. The dataset contains " +
            $"{All.Count} subdivisions. Use Find/TryGet for codes that may be absent.");

    public Iso3166CountrySubdivision? Find(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _store.Value.ByCode.TryGetValue(code, out var subdivision) ? subdivision : null;
    }

    public bool TryGet(string code, [NotNullWhen(true)] out Iso3166CountrySubdivision? subdivision)
    {
        subdivision = Find(code);
        return subdivision is not null;
    }

    public IReadOnlyList<Iso3166CountrySubdivision> ForCountry(string countryAlpha2)
    {
        ArgumentNullException.ThrowIfNull(countryAlpha2);
        return _store.Value.ByCountry.TryGetValue(countryAlpha2, out var list) ? list : [];
    }

    private static Iso3166CountrySubdivisionsStore Load()
    {
        var document = OpenDataResources.DeserializeJson(ResourceAssembly, Resource,
            Iso3166JsonContext.Default.Iso3166Part2Document);
        return Iso3166CountrySubdivisionsStore.Build(document.Entries);
    }
}