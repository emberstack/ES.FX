using System.Collections.Frozen;
using System.Globalization;
using ES.FX.OpenData.Countries.ISO3166;
using ES.FX.Primitives.Extensions;

namespace ES.FX.OpenData.Romania.TerritorialUnits.Internal;

internal sealed class RomanianTerritorialUnitsStore
{
    // Canonical CSV columns (unused REGIUNE/FSJ/NUTS dropped at ingest):
    // 0 SIRUTA; 1 DENLOC; 2 CODP; 3 JUD; 4 SIRSUP; 5 TIP; 6 NIV; 7 MED; 8 FSL
    private const int ColSiruta = 0,
        ColDenloc = 1,
        ColCodp = 2,
        ColJud = 3,
        ColSirsup = 4,
        ColTip = 5,
        ColNiv = 6,
        ColMed = 7,
        ColFsl = 8,
        MinColumns = 9;

    private const int VillageBelongingToCommuneTip = (int)SirutaUnitType.VillageBelongingToCommune;

    private RomanianTerritorialUnitsStore(
        IReadOnlyList<TerritorialUnit> allUnits,
        FrozenDictionary<int, TerritorialUnit> byCode,
        IReadOnlyList<TerritorialUnit> localities,
        IReadOnlyList<TerritorialUnit> searchLocalities,
        IReadOnlyList<TerritorialUnit> uats,
        IReadOnlyList<RomanianCounty> counties,
        FrozenDictionary<string, RomanianCounty> countiesByAbbreviation,
        FrozenDictionary<int, RomanianCounty> countiesBySiruta,
        FrozenDictionary<string, IReadOnlyList<TerritorialUnit>> localitiesByCounty,
        FrozenDictionary<string, IReadOnlyList<TerritorialUnit>> uatsByCounty,
        FrozenDictionary<int, IReadOnlyList<TerritorialUnit>> childrenByParent)
    {
        AllUnits = allUnits;
        ByCode = byCode;
        Localities = localities;
        SearchLocalities = searchLocalities;
        Uats = uats;
        Counties = counties;
        CountiesByAbbreviation = countiesByAbbreviation;
        CountiesBySiruta = countiesBySiruta;
        LocalitiesByCounty = localitiesByCounty;
        UatsByCounty = uatsByCounty;
        ChildrenByParent = childrenByParent;
    }

    public IReadOnlyList<TerritorialUnit> AllUnits { get; }
    public FrozenDictionary<int, TerritorialUnit> ByCode { get; }
    public IReadOnlyList<TerritorialUnit> Localities { get; }

    // Localities pre-sorted into Search's result order (SortingFactor, Name, SirutaCode) once at build time, so
    // Search filters this list and skips a per-call O(k log k) sort. Where preserves order, so the result matches.
    public IReadOnlyList<TerritorialUnit> SearchLocalities { get; }
    public IReadOnlyList<TerritorialUnit> Uats { get; }
    public IReadOnlyList<RomanianCounty> Counties { get; }
    public FrozenDictionary<string, RomanianCounty> CountiesByAbbreviation { get; }
    public FrozenDictionary<int, RomanianCounty> CountiesBySiruta { get; }
    public FrozenDictionary<string, IReadOnlyList<TerritorialUnit>> LocalitiesByCounty { get; }
    public FrozenDictionary<string, IReadOnlyList<TerritorialUnit>> UatsByCounty { get; }
    public FrozenDictionary<int, IReadOnlyList<TerritorialUnit>> ChildrenByParent { get; }

    public static RomanianTerritorialUnitsStore Build(
        IEnumerable<string[]> csvRows, IIso3166CountrySubdivisions subdivisions)
    {
        // Pass 1: parse rows and index raw names by code so a child's display name can reference its parent.
        var raw = new List<RawUnit>();
        var denlocByCode = new Dictionary<int, string>();
        foreach (var fields in csvRows)
        {
            if (fields.Length < MinColumns) continue;
            var code = ParseInt(fields, ColSiruta);
            var denloc = fields[ColDenloc];
            denlocByCode[code] = denloc;
            raw.Add(new RawUnit(
                code, denloc, fields[ColCodp],
                ParseInt(fields, ColJud),
                ParseInt(fields, ColSirsup),
                ParseInt(fields, ColTip),
                ParseInt(fields, ColNiv),
                ParseInt(fields, ColMed),
                fields[ColFsl]));
        }

        // Pass 2: materialize units, pooling the many duplicate name strings.
        var pool = new OpenDataStringPool();
        var all = new List<TerritorialUnit>(raw.Count);
        foreach (var r in raw)
        {
            var name = pool.Intern(r.Denloc.ToTitleCase());
            var displayName = r.Tip == VillageBelongingToCommuneTip &&
                              denlocByCode.TryGetValue(r.Sirsup, out var parentDenloc)
                ? pool.Intern($"{name} ({parentDenloc.ToTitleCase()})")
                : name;
            var abbreviation = SirutaCounties.ByJud.TryGetValue(r.Jud, out var abbr) ? abbr : string.Empty;
            // Strip diacritics once (the expensive Normalize step) and derive both normalized forms from the result.
            var diacriticFree = r.Denloc.RemoveDiacritics();

            all.Add(new TerritorialUnit
            {
                SirutaCode = r.Code,
                ParentSirutaCode = r.Sirsup,
                Name = name,
                DisplayName = displayName,
                SearchNormalizedName = pool.Intern(OpenDataText.FoldStripped(diacriticFree)),
                DisplayNormalizedName = pool.Intern(OpenDataText.NormalizeForDisplayStripped(diacriticFree)),
                Level = r.Niv,
                Type = (SirutaUnitType)r.Tip,
                AreaType = (AreaType)r.Med,
                PostalCode = NormalizePostalCode(r.Codp),
                SortingFactor = r.Fsl,
                CountyAbbreviation = pool.Intern(abbreviation)
            });
        }

        var byCode = all.ToFrozenDictionary(u => u.SirutaCode);
        var localities = all.Where(u => u.Level == 3).ToArray();
        // Sort the search corpus once here (was re-sorted on every Search call — an autocomplete hot path).
        var searchLocalities = localities
            .OrderBy(u => u.SortingFactor, StringComparer.Ordinal)
            .ThenBy(u => u.Name, StringComparer.Ordinal)
            .ThenBy(u => u.SirutaCode)
            .ToArray();
        var uats = all.Where(u => u.Level == 2).ToArray();

        var localitiesByCounty = GroupByCounty(localities);
        var uatsByCounty = GroupByCounty(uats);
        var childrenByParent = all
            .GroupBy(u => u.ParentSirutaCode)
            .ToFrozenDictionary(g => g.Key, g => (IReadOnlyList<TerritorialUnit>)Array.AsReadOnly(g.ToArray()));

        // County identity is DERIVED from SIRUTA — no curated side-table. The county set, ISO codes, and names
        // come from ES.FX.OpenData.Countries.ISO3166; a county's own SIRUTA code is its NIV=1 row, and its Residence links
        // to the UAT SIRUTA flags as the county seat (TIP 1 municipiu / TIP 5 oraș), or null for the counties with
        // no distinct seat unit (Ilfov, Bucharest).
        var countyRowByAbbreviation = all
            .Where(u => u.Level == 1)
            .ToDictionary(u => u.CountyAbbreviation, StringComparer.OrdinalIgnoreCase);
        var seatByAbbreviation = uats
            .Where(u => u.Type is SirutaUnitType.MunicipalityCountyResidence or SirutaUnitType.TownCountyResidence)
            .ToDictionary(u => u.CountyAbbreviation, StringComparer.OrdinalIgnoreCase);

        var counties = subdivisions.ForCountry("RO")
            .Select(subdivision =>
            {
                var abbreviation = subdivision.Code[(subdivision.Code.IndexOf('-') + 1)..];
                if (!countyRowByAbbreviation.TryGetValue(abbreviation, out var countyRow))
                    throw new InvalidOperationException(
                        $"SIRUTA has no level-1 county row for ISO 3166-2 subdivision '{subdivision.Code}'.");
                return new RomanianCounty
                {
                    SirutaCode = countyRow.SirutaCode,
                    Abbreviation = abbreviation,
                    IsoCountrySubdivision = subdivision,
                    IsoCode = subdivision.Code,
                    Name = subdivision.Name,
                    Residence = seatByAbbreviation.TryGetValue(abbreviation, out var seat) ? seat : null,
                    Localities = localitiesByCounty.TryGetValue(abbreviation, out var l) ? l : [],
                    Uats = uatsByCounty.TryGetValue(abbreviation, out var u) ? u : []
                };
            })
            .OrderBy(c => c.IsoCode, StringComparer.Ordinal)
            .ToArray();

        var countiesByAbbreviation = counties.ToFrozenDictionary(c => c.Abbreviation, StringComparer.OrdinalIgnoreCase);
        var countiesBySiruta = counties.ToFrozenDictionary(c => c.SirutaCode);

        return new RomanianTerritorialUnitsStore(
            Array.AsReadOnly(all.ToArray()), byCode, Array.AsReadOnly(localities), Array.AsReadOnly(searchLocalities),
            Array.AsReadOnly(uats), Array.AsReadOnly(counties), countiesByAbbreviation, countiesBySiruta,
            localitiesByCounty, uatsByCounty, childrenByParent);
    }

    private static FrozenDictionary<string, IReadOnlyList<TerritorialUnit>> GroupByCounty(
        IEnumerable<TerritorialUnit> units) =>
        units
            .Where(u => u.CountyAbbreviation.Length > 0)
            .GroupBy(u => u.CountyAbbreviation, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(
                g => g.Key,
                g => (IReadOnlyList<TerritorialUnit>)Array.AsReadOnly(g.ToArray()),
                StringComparer.OrdinalIgnoreCase);

    // Romanian postal codes are 6 digits. The embedded CSV stored CODP as an integer, so codes for the counties
    // whose codes begin with 0 (Bucharest, Ilfov, Giurgiu) arrive with the leading zero stripped — pad them back.
    // "0"/"" mean "no postal code" (counties and UAT-level rows) and map to null.
    private static string? NormalizePostalCode(string codp) =>
        codp is "0" or "" ? null : codp.PadLeft(6, '0');

    // Numeric SIRUTA columns come from a curated embedded resource; a non-numeric cell means the CSV was
    // regenerated wrong. Fail fast with the offending SIRUTA code and column instead of a bare FormatException.
    private static int ParseInt(string[] fields, int column) =>
        int.TryParse(fields[column], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException(
                $"SIRUTA row (code '{fields[ColSiruta]}') has a non-numeric value '{fields[column]}' in column {column}.");

    private readonly record struct RawUnit(
        int Code,
        string Denloc,
        string Codp,
        int Jud,
        int Sirsup,
        int Tip,
        int Niv,
        int Med,
        string Fsl);
}