using ES.FX.OpenData.Countries.ISO3166;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Romania.TerritorialUnits.Tests;

public class RomanianTerritorialUnitsDatasetTests
{
    private static readonly char[] CedillaCharacters = ['Ş', 'ş', 'Ţ', 'ţ'];

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddRomaniaTerritorialUnits();
        return services.BuildServiceProvider();
    }

    private static IRomanianTerritorialUnitsDataset Dataset() =>
        Provider().GetRequiredService<IRomanianTerritorialUnitsDataset>();

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
    public void Normalized_names_expose_a_search_form_and_a_diacritic_free_display_form()
    {
        var dataset = Dataset();

        // Diacritic case: search form is folded (lower, no diacritics); display-normalized is title-cased, no diacritics.
        var timisoara = dataset.Search("timisoara").First();
        Assert.Equal("Timișoara", timisoara.Name); // display keeps diacritics
        Assert.Equal("timisoara", timisoara.SearchNormalizedName); // search: folded
        Assert.Equal("Timisoara", timisoara.DisplayNormalizedName); // display-normalized: ASCII, title-cased

        // Hyphen case: the search form spaces the hyphen; the display-normalized form keeps it.
        var clujNapoca = dataset.Search("cluj napoca").First();
        Assert.Equal("Cluj-Napoca", clujNapoca.Name);
        Assert.Equal("cluj napoca", clujNapoca.SearchNormalizedName);
        Assert.Equal("Cluj-Napoca", clujNapoca.DisplayNormalizedName);
    }

    [Fact]
    public void Find_resolves_any_level_and_never_throws_for_a_county()
    {
        // Regression guard: looking up a non-locality code (e.g. a county) used to throw. It must not.
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

        static int[] Codes(IEnumerable<TerritorialUnit> units) => units.Select(u => u.SirutaCode).ToArray();

        var hyphen = Codes(dataset.Search("cluj-napoca"));
        Assert.NotEmpty(hyphen);
        Assert.Equal(hyphen, Codes(dataset.Search("cluj napoca"))); // hyphen ≡ space
        Assert.Equal(hyphen, Codes(dataset.Search("CLUJ-NAPOCA"))); // case-insensitive

        var diacritic = Codes(dataset.Search("timiș"));
        Assert.NotEmpty(diacritic);
        Assert.Equal(diacritic, Codes(dataset.Search("timis"))); // comma-below ≡ ascii
    }

    [Fact]
    public void Indexer_throws_for_unknown_code() =>
        Assert.Throws<KeyNotFoundException>(() => Dataset()[-1]);


    // ----- County ISO 3166-2 identity is sourced from ES.FX.OpenData.Countries.ISO3166 (single source) -----

    [Fact]
    public void County_iso_3166_2_data_is_sourced_from_the_iso3166_dataset()
    {
        var provider = Provider();
        var dataset = provider.GetRequiredService<IRomanianTerritorialUnitsDataset>();
        var subdivisions = provider.GetRequiredService<IIso3166CountrySubdivisions>();

        Assert.All(dataset.Counties, county =>
        {
            var subdivision = subdivisions["RO-" + county.Abbreviation];
            Assert.Same(subdivision, county.IsoCountrySubdivision); // linked to the ISO 3166-2 object, not a copy
            Assert.Equal(subdivision.Code, county.IsoCode); // IsoCode projected from ISO 3166-2
            Assert.Equal(subdivision.Name, county.Name); // Name projected from ISO 3166-2
        });
    }

    [Fact]
    public void Counties_map_one_to_one_to_the_iso_3166_2_ro_subdivisions()
    {
        var provider = Provider();
        var dataset = provider.GetRequiredService<IRomanianTerritorialUnitsDataset>();
        var subdivisions = provider.GetRequiredService<IIso3166CountrySubdivisions>();

        var roSubdivisionCodes = subdivisions.ForCountry("RO").Select(s => s.Code).Order().ToArray();
        var countyIsoCodes = dataset.Counties.Select(c => c.IsoCode).Order().ToArray();

        Assert.Equal(42, roSubdivisionCodes.Length);
        Assert.Equal(roSubdivisionCodes, countyIsoCodes); // exact 1:1 — counties can never drift from ISO
    }

    [Fact]
    public void County_abbreviation_is_the_iso_3166_2_suffix_and_the_auto_code()
    {
        var cluj = Dataset().FindCounty("CJ")!;
        Assert.Equal("CJ", cluj.Abbreviation); // the "cod auto" / plate code
        Assert.Equal("RO-CJ", cluj.IsoCode); // == "RO-" + Abbreviation
        Assert.Equal("Department", cluj.IsoCountrySubdivision.Type); // ISO 3166-2 subdivision category
    }


    // ----- Behavior locked by the review pass -----

    [Fact]
    public void Area_type_maps_urban_and_rural_from_siruta_med()
    {
        // SIRUTA MED is {0 county, 1 urban, 3 rural} — Rural = 3 is non-obvious, so pin it.
        var dataset = Dataset();
        Assert.Equal(AreaType.Urban, dataset[1026].AreaType); // ALBA IULIA (MED 1)
        Assert.Equal(AreaType.Rural, dataset[2158].AreaType); // BĂRĂȘTI (MED 3)
        Assert.Equal(AreaType.None, dataset[10].AreaType); // JUDEȚUL ALBA, a county (MED 0)
    }

    [Fact]
    public void Exposed_collections_are_read_only_not_mutable_arrays()
    {
        var dataset = Dataset();
        var cluj = dataset.FindCounty("CJ")!;

        // These were raw arrays/lists before hardening — castable and index-mutable on a shared singleton.
        Assert.Throws<NotSupportedException>(() =>
            ((IList<TerritorialUnit>)dataset.Localities)[0] = dataset.Localities[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<RomanianCounty>)dataset.Counties)[0] = cluj);
        Assert.Throws<NotSupportedException>(() => ((IList<TerritorialUnit>)cluj.Localities)[0] = cluj.Localities[0]);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<TerritorialUnit>)dataset.GetLocalitiesInCounty("BH"))[0] = cluj.Localities[0]);
    }

    [Fact]
    public void Search_with_a_blank_prefix_returns_empty()
    {
        var dataset = Dataset();
        Assert.Empty(dataset.Search(""));
        Assert.Empty(dataset.Search("   "));
    }

    [Fact]
    public void Unknown_county_lookups_are_empty_or_null_never_throwing()
    {
        var dataset = Dataset();
        Assert.Null(dataset.FindCounty("ZZ"));
        Assert.Empty(dataset.GetLocalitiesInCounty("ZZ"));
    }

    [Fact]
    public void TryGet_reports_presence()
    {
        var dataset = Dataset();
        Assert.True(dataset.TryGet(1026, out var found));
        Assert.Equal("Alba Iulia", found!.Name);
        Assert.False(dataset.TryGet(-1, out var missing));
        Assert.Null(missing);
    }


    // ----- Behavior added/fixed by the workflow review pass -----

    [Fact]
    public void Postal_codes_keep_their_leading_zero_as_six_digits()
    {
        var dataset = Dataset();
        Assert.Equal("080303", dataset[100530].PostalCode); // Giurgiu-county locality (CSV shipped "80303")
        Assert.Equal("010013", dataset[179141].PostalCode); // București Sectorul 1 (CSV shipped "10013")
        Assert.Null(dataset[10].PostalCode); // a county carries no postal code
    }

    [Fact]
    public void Uats_collection_is_all_level_2()
    {
        var dataset = Dataset();
        Assert.Equal(3181, dataset.Uats.Count);
        Assert.All(dataset.Uats, u => Assert.Equal(2, u.Level));
    }

    [Fact]
    public void Bucharest_sectors_are_the_six_level_3_localities_of_county_B()
    {
        var sectors = Dataset().GetLocalitiesInCounty("B");
        Assert.Equal(6, sectors.Count);
        Assert.All(sectors, s => Assert.Equal(3, s.Level));
    }

    [Fact]
    public void Search_returns_a_materialized_list_of_localities_only()
    {
        var dataset = Dataset();
        var hits = dataset.Search("cluj");
        Assert.NotEmpty(hits);
        Assert.All(hits, u => Assert.Equal(3, u.Level)); // localities only
        Assert.Equal(hits, dataset.Search("cluj")); // materialized: stable across calls
    }

    [Fact]
    public void GetChildren_returns_direct_descendants_only()
    {
        var dataset = Dataset();

        var albaUats = dataset.GetChildren(10); // JUDEȚUL ALBA -> its UATs
        Assert.NotEmpty(albaUats);
        Assert.All(albaUats, u =>
        {
            Assert.Equal(2, u.Level);
            Assert.Equal(10, u.ParentSirutaCode);
        });

        var albaIuliaLocalities = dataset.GetChildren(1017); // MUNICIPIUL ALBA IULIA -> its localities
        Assert.Contains(albaIuliaLocalities, u => u.SirutaCode == 1026);
        Assert.All(albaIuliaLocalities, u => Assert.Equal(1017, u.ParentSirutaCode));

        Assert.Empty(dataset.GetChildren(-1)); // unknown/leaf
    }

    [Fact]
    public void GetUatsInCounty_mirrors_the_localities_helper_and_the_county_property()
    {
        var dataset = Dataset();
        var uats = dataset.GetUatsInCounty("AB");
        Assert.NotEmpty(uats);
        Assert.All(uats, u =>
        {
            Assert.Equal(2, u.Level);
            Assert.Equal("AB", u.CountyAbbreviation);
        });
        Assert.Equal(uats.Count, dataset.GetUatsInCounty("RO-AB").Count); // iso == abbreviation
        Assert.Same(uats, dataset.FindCounty("AB")!.Uats); // property is the same collection
        Assert.Empty(dataset.GetUatsInCounty("ZZ")); // unknown county
    }

    [Fact]
    public void FindCounty_by_siruta_code_resolves_the_enriched_county()
    {
        var dataset = Dataset();
        var alba = dataset.FindCounty(10); // Alba county's own NIV=1 SIRUTA code is 10
        Assert.NotNull(alba);
        Assert.Equal("RO-AB", alba!.IsoCode);
        Assert.Same(alba, dataset.FindCounty("AB")); // same enriched instance as the string lookup
        Assert.Null(dataset.FindCounty(1026)); // a locality code is not a county
    }

    [Fact]
    public void GetParent_walks_up_and_stops_at_the_national_root()
    {
        var dataset = Dataset();
        var uat = dataset.GetParent(dataset[1026]); // ALBA IULIA locality -> its UAT
        Assert.NotNull(uat);
        Assert.Equal(1017, uat!.SirutaCode);
        Assert.Equal(2, uat.Level);
        Assert.Null(dataset.GetParent(dataset[10])); // a county's parent is out-of-dataset
    }

    [Fact]
    public void GetCounty_resolves_a_units_enriched_county()
    {
        var dataset = Dataset();
        var county = dataset.GetCounty(dataset[1026]); // ALBA IULIA locality -> Alba county
        Assert.NotNull(county);
        Assert.Equal("AB", county!.Abbreviation);
        Assert.Same(county, dataset.FindCounty("AB"));
    }

    [Fact]
    public void County_residence_links_to_the_siruta_seat_unit()
    {
        var dataset = Dataset();

        // The residence is the SIRUTA unit flagged as the county seat (TIP 1 municipiu / TIP 5 oraș) — a live
        // link into the dataset, not a copied name.
        var cluj = dataset.FindCounty("CJ")!;
        Assert.NotNull(cluj.Residence);
        Assert.Equal(SirutaUnitType.MunicipalityCountyResidence, cluj.Residence!.Type);
        Assert.Equal("Municipiul Cluj-Napoca", cluj.Residence.Name);
        Assert.Same(cluj.Residence, dataset[cluj.Residence.SirutaCode]); // same instance, not a copy

        // Counties with no distinct seat unit in SIRUTA have a null residence.
        Assert.Null(dataset.FindCounty("IF")!.Residence); // Ilfov
        Assert.Null(dataset.FindCounty("B")!.Residence); // Bucharest

        // Every other county resolves to a seat.
        Assert.All(dataset.Counties.Where(c => c.Abbreviation is not ("IF" or "B")),
            c => Assert.NotNull(c.Residence));
    }
}