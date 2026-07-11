using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ES.FX.OpenData.Countries.ISO3166.Internal;

internal sealed class Iso3166FormerCountriesDataset : IIso3166FormerCountries
{
    private const string Resource = "ES.FX.OpenData.Countries.ISO3166.iso_3166-3.json";
    private static readonly Assembly ResourceAssembly = typeof(Iso3166FormerCountriesDataset).Assembly;

    private readonly Lazy<Iso3166FormerCountriesStore> _store = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<Iso3166FormerCountry> All => _store.Value.All;

    public Iso3166FormerCountry this[string alpha4] =>
        Find(alpha4) ?? throw new KeyNotFoundException(
            $"Unknown ISO 3166-3 four-letter code '{alpha4}'. The dataset contains " +
            $"{All.Count} formerly used codes. Use Find/TryGet for codes that may be absent.");

    public Iso3166FormerCountry? Find(string alpha4)
    {
        ArgumentNullException.ThrowIfNull(alpha4);
        return _store.Value.ByAlpha4.TryGetValue(alpha4, out var former) ? former : null;
    }

    public bool TryGet(string alpha4, [NotNullWhen(true)] out Iso3166FormerCountry? formerCountry)
    {
        formerCountry = Find(alpha4);
        return formerCountry is not null;
    }

    public Iso3166FormerCountry? FindByAlpha2(string alpha2)
    {
        ArgumentNullException.ThrowIfNull(alpha2);
        return _store.Value.ByAlpha2.TryGetValue(alpha2, out var former) ? former : null;
    }

    private static Iso3166FormerCountriesStore Load()
    {
        var document = OpenDataResources.DeserializeJson(ResourceAssembly, Resource,
            Iso3166JsonContext.Default.Iso3166Part3Document);
        return Iso3166FormerCountriesStore.Build(document.Entries);
    }
}