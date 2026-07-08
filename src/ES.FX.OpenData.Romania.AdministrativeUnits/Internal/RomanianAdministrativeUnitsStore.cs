using System.Collections.Frozen;
using System.Globalization;
using ES.FX.Primitives.Extensions;

namespace ES.FX.OpenData.Romania.AdministrativeUnits.Internal;

internal sealed class RomanianAdministrativeUnitsStore
{
    // Canonical CSV columns (unused REGIUNE/FSJ/NUTS dropped at ingest):
    // 0 SIRUTA; 1 DENLOC; 2 CODP; 3 JUD; 4 SIRSUP; 5 TIP; 6 NIV; 7 MED; 8 FSL
    private const int ColSiruta = 0, ColDenloc = 1, ColCodp = 2, ColJud = 3, ColSirsup = 4,
        ColTip = 5, ColNiv = 6, ColMed = 7, ColFsl = 8, MinColumns = 9;

    private const int VillageBelongingToCommuneTip = (int)SirutaUnitType.VillageBelongingToCommune;

    private RomanianAdministrativeUnitsStore(
        IReadOnlyList<AdministrativeUnit> allUnits,
        FrozenDictionary<int, AdministrativeUnit> byCode,
        IReadOnlyList<AdministrativeUnit> localities,
        IReadOnlyList<AdministrativeUnit> uats,
        IReadOnlyList<RomanianCounty> counties,
        FrozenDictionary<string, RomanianCounty> countiesByAbbreviation,
        FrozenDictionary<string, AdministrativeUnit[]> localitiesByCounty)
    {
        AllUnits = allUnits;
        ByCode = byCode;
        Localities = localities;
        Uats = uats;
        Counties = counties;
        CountiesByAbbreviation = countiesByAbbreviation;
        LocalitiesByCounty = localitiesByCounty;
    }

    public IReadOnlyList<AdministrativeUnit> AllUnits { get; }
    public FrozenDictionary<int, AdministrativeUnit> ByCode { get; }
    public IReadOnlyList<AdministrativeUnit> Localities { get; }
    public IReadOnlyList<AdministrativeUnit> Uats { get; }
    public IReadOnlyList<RomanianCounty> Counties { get; }
    public FrozenDictionary<string, RomanianCounty> CountiesByAbbreviation { get; }
    public FrozenDictionary<string, AdministrativeUnit[]> LocalitiesByCounty { get; }

    public static RomanianAdministrativeUnitsStore Build(
        IEnumerable<string[]> csvRows, IReadOnlyList<CountyRow> countyRows)
    {
        // Pass 1: parse rows and index raw names by code so a child's display name can reference its parent.
        var raw = new List<RawUnit>();
        var denlocByCode = new Dictionary<int, string>();
        foreach (var fields in csvRows)
        {
            if (fields.Length < MinColumns) continue;
            var code = int.Parse(fields[ColSiruta], CultureInfo.InvariantCulture);
            var denloc = fields[ColDenloc];
            denlocByCode[code] = denloc;
            raw.Add(new RawUnit(
                code, denloc, fields[ColCodp],
                int.Parse(fields[ColJud], CultureInfo.InvariantCulture),
                int.Parse(fields[ColSirsup], CultureInfo.InvariantCulture),
                int.Parse(fields[ColTip], CultureInfo.InvariantCulture),
                int.Parse(fields[ColNiv], CultureInfo.InvariantCulture),
                int.Parse(fields[ColMed], CultureInfo.InvariantCulture),
                fields[ColFsl]));
        }

        // Pass 2: materialize units, pooling the many duplicate name strings.
        var pool = new OpenDataStringPool();
        var all = new List<AdministrativeUnit>(raw.Count);
        foreach (var r in raw)
        {
            var name = pool.Intern(r.Denloc.ToTitleCase());
            var displayName = r.Tip == VillageBelongingToCommuneTip &&
                              denlocByCode.TryGetValue(r.Sirsup, out var parentDenloc)
                ? pool.Intern($"{name} ({parentDenloc.ToTitleCase()})")
                : name;
            var abbreviation = SirutaCounties.ByJud.TryGetValue(r.Jud, out var abbr) ? abbr : string.Empty;

            all.Add(new AdministrativeUnit
            {
                SirutaCode = r.Code,
                ParentSirutaCode = r.Sirsup,
                Name = name,
                DisplayName = displayName,
                NormalizedName = pool.Intern(OpenDataText.Fold(r.Denloc)),
                Level = r.Niv,
                Type = (SirutaUnitType)r.Tip,
                AreaType = (AreaType)r.Med,
                PostalCode = r.Codp is "0" or "" ? string.Empty : r.Codp,
                SortingFactor = r.Fsl,
                CountyAbbreviation = pool.Intern(abbreviation)
            });
        }

        var byCode = all.ToFrozenDictionary(u => u.SirutaCode);
        var localities = all.Where(u => u.Level == 3).ToArray();
        var uats = all.Where(u => u.Level == 2).ToArray();

        var localitiesByCounty = localities
            .Where(u => u.CountyAbbreviation.Length > 0)
            .GroupBy(u => u.CountyAbbreviation, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var counties = countyRows.Select(c => new RomanianCounty
        {
            SirutaCode = c.SirutaCode,
            IsoCode = c.IsoCode,
            Abbreviation = c.Abbreviation,
            Name = c.Name,
            ResidenceName = c.ResidenceName,
            NationalIdSeries = c.NationalIdSeries,
            Localities = localitiesByCounty.TryGetValue(c.Abbreviation, out var l) ? l : []
        }).ToArray();

        var countiesByAbbreviation = counties.ToFrozenDictionary(c => c.Abbreviation, StringComparer.OrdinalIgnoreCase);

        return new RomanianAdministrativeUnitsStore(
            all, byCode, localities, uats, counties, countiesByAbbreviation, localitiesByCounty);
    }

    private readonly record struct RawUnit(
        int Code, string Denloc, string Codp, int Jud, int Sirsup, int Tip, int Niv, int Med, string Fsl);
}
