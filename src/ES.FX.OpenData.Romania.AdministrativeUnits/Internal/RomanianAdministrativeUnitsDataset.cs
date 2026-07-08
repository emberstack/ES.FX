using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ES.FX.OpenData.Romania.AdministrativeUnits.Internal;

internal sealed class RomanianAdministrativeUnitsDataset : IRomanianAdministrativeUnitsDataset
{
    internal static readonly OpenDatasetInfo DatasetInfo = new()
    {
        Name = "SIRUTA",
        Edition = "2025-12",
        Source = "INS (Institutul Național de Statistică)",
        SourceUrl = "https://www.insse.ro/cms/ro/content/siruta",
        License = "ES.FX.OpenData code under MIT; SIRUTA data published by INS as public open data.",
        Standard = "SIRUTA"
    };

    private const string CsvResource = "ES.FX.OpenData.Romania.AdministrativeUnits.siruta.2025-12.csv.gz";
    private const string CountiesResource = "ES.FX.OpenData.Romania.AdministrativeUnits.counties.json";

    private static readonly Assembly ResourceAssembly = typeof(RomanianAdministrativeUnitsDataset).Assembly;

    private readonly Lazy<RomanianAdministrativeUnitsStore> _store =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public OpenDatasetInfo Info => DatasetInfo;

    public void EnsureLoaded() => _ = _store.Value;

    public IReadOnlyList<AdministrativeUnit> AllUnits => _store.Value.AllUnits;
    public IReadOnlyList<AdministrativeUnit> Localities => _store.Value.Localities;
    public IReadOnlyList<AdministrativeUnit> Uats => _store.Value.Uats;
    public IReadOnlyList<RomanianCounty> Counties => _store.Value.Counties;

    public AdministrativeUnit this[int sirutaCode] =>
        Find(sirutaCode) ?? throw new KeyNotFoundException(
            $"Unknown SIRUTA code {sirutaCode}. The SIRUTA dataset (edition {DatasetInfo.Edition}) contains " +
            $"{AllUnits.Count} units. Use Find/TryGet for codes that may be absent.");

    public AdministrativeUnit? Find(int sirutaCode) =>
        _store.Value.ByCode.TryGetValue(sirutaCode, out var unit) ? unit : null;

    public bool TryGet(int sirutaCode, [NotNullWhen(true)] out AdministrativeUnit? unit)
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

    public IReadOnlyList<AdministrativeUnit> GetLocalitiesInCounty(string isoOrAbbreviation)
    {
        ArgumentNullException.ThrowIfNull(isoOrAbbreviation);
        return _store.Value.LocalitiesByCounty.TryGetValue(NormalizeCounty(isoOrAbbreviation), out var localities)
            ? localities
            : [];
    }

    public IEnumerable<AdministrativeUnit> Search(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var folded = OpenDataText.Fold(prefix);
        if (folded.Length == 0) return [];
        return _store.Value.Localities
            .Where(u => u.NormalizedName.StartsWith(folded, StringComparison.Ordinal))
            .OrderBy(u => u.SortingFactor, StringComparer.Ordinal)
            .ThenBy(u => u.Name, StringComparer.Ordinal)
            .ThenBy(u => u.SirutaCode);
    }

    private static string NormalizeCounty(string code)
    {
        var trimmed = code.Trim();
        return trimmed.StartsWith("RO-", StringComparison.OrdinalIgnoreCase) ? trimmed[3..] : trimmed;
    }

    private static RomanianAdministrativeUnitsStore Load()
    {
        var csv = OpenDataResources.ReadGzipDelimitedLines(ResourceAssembly, CsvResource, ';', skipHeader: true);
        var counties = OpenDataResources.DeserializeJson(
            ResourceAssembly, CountiesResource, RomanianAdministrativeUnitsJsonContext.Default.CountyRowArray);
        return RomanianAdministrativeUnitsStore.Build(csv, counties);
    }
}
