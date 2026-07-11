using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ES.FX.OpenData.Countries.ISO3166;

namespace ES.FX.OpenData.Romania.TerritorialUnits.Internal;

internal sealed class RomanianTerritorialUnitsDataset : IRomanianTerritorialUnitsDataset
{
    private const string Edition = "2025-12";
    private const string CsvResource = "ES.FX.OpenData.Romania.TerritorialUnits.siruta.2025-12.csv.gz";

    private static readonly Assembly ResourceAssembly = typeof(RomanianTerritorialUnitsDataset).Assembly;
    private readonly Lazy<RomanianTerritorialUnitsStore> _store;

    private readonly IIso3166CountrySubdivisions _subdivisions;

    public RomanianTerritorialUnitsDataset(IIso3166CountrySubdivisions subdivisions)
    {
        _subdivisions = subdivisions;
        _store = new Lazy<RomanianTerritorialUnitsStore>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyList<TerritorialUnit> AllUnits => _store.Value.AllUnits;
    public IReadOnlyList<TerritorialUnit> Localities => _store.Value.Localities;
    public IReadOnlyList<TerritorialUnit> Uats => _store.Value.Uats;
    public IReadOnlyList<RomanianCounty> Counties => _store.Value.Counties;

    public TerritorialUnit this[int sirutaCode] =>
        Find(sirutaCode) ?? throw new KeyNotFoundException(
            $"Unknown SIRUTA code {sirutaCode}. The SIRUTA dataset (edition {Edition}) contains " +
            $"{AllUnits.Count} units. Use Find/TryGet for codes that may be absent.");

    public TerritorialUnit? Find(int sirutaCode) =>
        _store.Value.ByCode.TryGetValue(sirutaCode, out var unit) ? unit : null;

    public bool TryGet(int sirutaCode, [NotNullWhen(true)] out TerritorialUnit? unit)
    {
        unit = Find(sirutaCode);
        return unit is not null;
    }

    public RomanianCounty? FindCounty(string isoOrAbbreviation)
    {
        ArgumentNullException.ThrowIfNull(isoOrAbbreviation);
        return _store.Value.CountiesByAbbreviation.TryGetValue(NormalizeCounty(isoOrAbbreviation), out var county)
            ? county
            : null;
    }

    public RomanianCounty? FindCounty(int sirutaCode) =>
        _store.Value.CountiesBySiruta.TryGetValue(sirutaCode, out var county) ? county : null;

    public IReadOnlyList<TerritorialUnit> GetLocalitiesInCounty(string isoOrAbbreviation)
    {
        ArgumentNullException.ThrowIfNull(isoOrAbbreviation);
        return _store.Value.LocalitiesByCounty.TryGetValue(NormalizeCounty(isoOrAbbreviation), out var localities)
            ? localities
            : [];
    }

    public IReadOnlyList<TerritorialUnit> GetUatsInCounty(string isoOrAbbreviation)
    {
        ArgumentNullException.ThrowIfNull(isoOrAbbreviation);
        return _store.Value.UatsByCounty.TryGetValue(NormalizeCounty(isoOrAbbreviation), out var uats)
            ? uats
            : [];
    }

    public IReadOnlyList<TerritorialUnit> GetChildren(int sirutaCode) =>
        _store.Value.ChildrenByParent.TryGetValue(sirutaCode, out var children) ? children : [];

    public TerritorialUnit? GetParent(TerritorialUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return Find(unit.ParentSirutaCode);
    }

    public RomanianCounty? GetCounty(TerritorialUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return unit.CountyAbbreviation.Length == 0 ? null : FindCounty(unit.CountyAbbreviation);
    }

    public IReadOnlyList<TerritorialUnit> Search(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var folded = OpenDataText.Fold(prefix);
        if (folded.Length == 0) return [];
        // SearchLocalities is pre-sorted into result order at build time and Where preserves order, so filtering
        // it yields the same result the per-call OrderBy(SortingFactor).ThenBy(Name).ThenBy(SirutaCode) produced.
        return _store.Value.SearchLocalities
            .Where(u => u.SearchNormalizedName.StartsWith(folded, StringComparison.Ordinal))
            .ToArray();
    }

    public void EnsureLoaded() => _ = _store.Value;

    private static string NormalizeCounty(string code)
    {
        var trimmed = code.Trim();
        return trimmed.StartsWith("RO-", StringComparison.OrdinalIgnoreCase) ? trimmed[3..] : trimmed;
    }

    private RomanianTerritorialUnitsStore Load()
    {
        var csv = OpenDataResources.ReadGzipDelimitedLines(ResourceAssembly, CsvResource, ';', true);
        return RomanianTerritorialUnitsStore.Build(csv, _subdivisions);
    }
}