using ES.FX.OpenData;
using ES.FX.OpenData.Romania.AdministrativeUnits;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Romania.AdministrativeUnits.Tests;

public class RomanianAdministrativeUnitsDatasetTests
{
    private static readonly char[] CedillaCharacters = ['Ş', 'ş', 'Ţ', 'ţ'];

    private static IRomanianAdministrativeUnitsDataset Dataset()
    {
        var services = new ServiceCollection();
        services.AddOpenData().AddRomaniaAdministrativeUnits();
        return services.BuildServiceProvider().GetRequiredService<IRomanianAdministrativeUnitsDataset>();
    }

    [Fact]
    public void Counts_match_the_edition()
    {
        var dataset = Dataset();
        Assert.Equal(16978, dataset.AllUnits.Count);
        Assert.Equal(13755, dataset.Localities.Count);
    }

    [Fact]
    public void Names_are_title_cased_not_all_caps()
    {
        // The whole point: raw SIRUTA DENLOC is ALL CAPS; the dataset serves clean names.
        var albaIulia = Dataset()[1026];
        Assert.Equal("Alba Iulia", albaIulia.Name);
        Assert.Equal("Alba Iulia", albaIulia.DisplayName);
        Assert.Equal(3, albaIulia.Level);
    }

    [Fact]
    public void Find_resolves_any_level_and_never_throws_for_a_county()
    {
        // The selfpay landmine: looking up a non-locality code used to throw. It must not.
        var dataset = Dataset();
        var county = dataset.Find(10); // JUDEȚUL ALBA (level 1, TIP 40)
        Assert.NotNull(county);
        Assert.Equal(1, county!.Level);
        Assert.Equal(SirutaUnitType.County, county.Type);
        Assert.Same(county, dataset[10]); // indexer agrees, still no throw
    }

    [Fact]
    public void GetLocalitiesInCounty_returns_the_expected_count_by_abbreviation_and_iso()
    {
        var dataset = Dataset();
        Assert.Equal(458, dataset.GetLocalitiesInCounty("BH").Count);
        Assert.Equal(458, dataset.GetLocalitiesInCounty("RO-BH").Count);
        Assert.Equal(458, dataset.GetLocalitiesInCounty("bh").Count);
    }

    [Fact]
    public void Counties_are_complete_and_enriched()
    {
        var dataset = Dataset();
        Assert.Equal(42, dataset.Counties.Count);

        var cluj = dataset.FindCounty("CJ");
        Assert.NotNull(cluj);
        Assert.Same(cluj, dataset.FindCounty("RO-CJ"));
        Assert.Equal("RO-CJ", cluj!.IsoCode);
        Assert.NotEmpty(cluj.NationalIdSeries);
        Assert.NotEmpty(cluj.Localities);
    }

    [Fact]
    public void Villages_belonging_to_a_commune_get_a_disambiguated_display_name()
    {
        var unit = Dataset()[2158]; // BĂRĂȘTI, child of commune ALBAC
        Assert.Equal(SirutaUnitType.VillageBelongingToCommune, unit.Type);
        Assert.Equal("Bărăști", unit.Name);
        Assert.Equal("Bărăști (Albac)", unit.DisplayName);
        Assert.NotEqual(unit.Name, unit.DisplayName);
    }

    [Fact]
    public void Names_use_comma_below_never_legacy_cedilla()
    {
        var dataset = Dataset();

        foreach (var unit in dataset.AllUnits)
        {
            Assert.Equal(-1, unit.Name.IndexOfAny(CedillaCharacters));
            Assert.Equal(-1, unit.DisplayName.IndexOfAny(CedillaCharacters));
        }

        // And diacritics are genuinely preserved (comma-below ș = U+0219).
        var timisoara = dataset.Search("timisoara").First();
        Assert.Equal("Timișoara", timisoara.Name);
        Assert.Contains('ș', timisoara.Name);
    }

    [Fact]
    public void Search_is_case_diacritic_and_hyphen_insensitive()
    {
        var dataset = Dataset();

        static int[] Codes(IEnumerable<AdministrativeUnit> units) => units.Select(u => u.SirutaCode).ToArray();

        var hyphen = Codes(dataset.Search("cluj-napoca"));
        Assert.NotEmpty(hyphen);
        Assert.Equal(hyphen, Codes(dataset.Search("cluj napoca")));   // hyphen ≡ space
        Assert.Equal(hyphen, Codes(dataset.Search("CLUJ-NAPOCA")));   // case-insensitive

        var diacritic = Codes(dataset.Search("timiș"));
        Assert.NotEmpty(diacritic);
        Assert.Equal(diacritic, Codes(dataset.Search("timis")));      // comma-below ≡ ascii
    }

    [Fact]
    public void Indexer_throws_for_unknown_code() =>
        Assert.Throws<KeyNotFoundException>(() => Dataset()[-1]);

    [Fact]
    public void Info_describes_the_dataset()
    {
        var info = Dataset().Info;
        Assert.Equal("SIRUTA", info.Name);
        Assert.Equal("2025-12", info.Edition);
    }
}
