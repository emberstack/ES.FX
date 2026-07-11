using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Countries.ISO3166.Tests;

public class Iso3166DatasetTests
{
    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddIso3166();
        return services.BuildServiceProvider();
    }

    private static IIso3166 Iso3166() => Provider().GetRequiredService<IIso3166>();

    // ----- Part 1: country codes -----

    [Fact]
    public void Part1_has_the_expected_count() =>
        Assert.Equal(249, Iso3166().Countries.All.Count);

    [Fact]
    public void Part1_indexer_is_case_insensitive_and_maps_codes()
    {
        var countries = Iso3166().Countries;
        var ro = countries["ro"];
        Assert.Equal("RO", ro.Alpha2);
        Assert.Equal("ROU", ro.Alpha3);
        Assert.Equal(642, ro.NumericCode);
        Assert.Equal("Romania", ro.Name);
    }

    [Fact]
    public void Part1_indexer_throws_for_unknown_code() =>
        Assert.Throws<KeyNotFoundException>(() => Iso3166().Countries["ZZ"]);

    [Fact]
    public void Part1_find_variants_resolve_by_alpha3_and_numeric()
    {
        var countries = Iso3166().Countries;
        Assert.Equal("RO", countries.FindByAlpha3("ROU")?.Alpha2);
        Assert.Equal("RO", countries.FindByNumericCode(642)?.Alpha2);
        Assert.Null(countries.FindByAlpha3("ZZZ"));
        Assert.Null(countries.FindByNumericCode(9999));
    }

    [Fact]
    public void Part1_exposes_official_and_common_names_and_flags()
    {
        var countries = Iso3166().Countries;
        Assert.Equal("Islamic Republic of Afghanistan", countries["AF"].OfficialName);
        Assert.Equal("South Korea", countries["KR"].CommonName);
        Assert.Equal("🇷🇴", countries["RO"].Flag);
        Assert.Null(countries["RO"].OfficialName); // no distinct official name
    }

    // ----- Part 2: subdivision codes -----

    [Fact]
    public void Part2_has_the_expected_count() =>
        Assert.Equal(5046, Iso3166().CountrySubdivisions.All.Count);

    [Fact]
    public void Part2_resolves_hawaii_and_derives_country_prefix()
    {
        var subdivisions = Iso3166().CountrySubdivisions;
        var hawaii = subdivisions["US-HI"];
        Assert.Equal("Hawaii", hawaii.Name);
        Assert.Equal("State", hawaii.Type);
        Assert.Equal("US", hawaii.CountryAlpha2);
        Assert.Null(hawaii.Parent);
    }

    [Fact]
    public void Part2_for_country_returns_all_subdivisions_of_a_country()
    {
        var subdivisions = Iso3166().CountrySubdivisions;
        var usStates = subdivisions.ForCountry("US");
        Assert.Contains(usStates, s => s.Code == "US-HI");
        Assert.All(usStates, s => Assert.Equal("US", s.CountryAlpha2));
        Assert.NotEmpty(usStates);
        Assert.Empty(subdivisions.ForCountry("ZZ")); // unknown country → empty, not throw
    }

    [Fact]
    public void Part2_nested_subdivisions_carry_their_parent()
    {
        // Azerbaijani rayons nest under the Nakhchivan autonomous republic (AZ-NX).
        var babek = Iso3166().CountrySubdivisions["AZ-BAB"];
        Assert.Equal("AZ-NX", babek.Parent);
        Assert.Equal("AZ", babek.CountryAlpha2);
    }

    // ----- Part 3: formerly used country codes -----

    [Fact]
    public void Part3_has_the_expected_count() =>
        Assert.Equal(31, Iso3166().FormerCountries.All.Count);

    [Fact]
    public void Part3_resolves_netherlands_antilles_by_four_letter_code()
    {
        var former = Iso3166().FormerCountries["ANHH"];
        Assert.Equal("Netherlands Antilles", former.Name);
        Assert.Equal("AN", former.Alpha2);
        Assert.Equal("ANT", former.Alpha3);
        Assert.Equal(530, former.NumericCode);
        Assert.Equal("2010-12-15", former.WithdrawalDate);
        Assert.NotNull(former.Comment);
    }

    [Fact]
    public void Part3_find_by_alpha2_resolves_retired_code() =>
        Assert.Equal("ANHH", Iso3166().FormerCountries.FindByAlpha2("AN")?.Alpha4);

    [Fact]
    public void Part3_tolerates_missing_numeric_codes()
    {
        // British Antarctic Territory (BQAQ) has no numeric code in ISO 3166-3.
        var former = Iso3166().FormerCountries["BQAQ"];
        Assert.Null(former.NumericCode);
        Assert.Equal("British Antarctic Territory", former.Name);
    }

    // ----- Cross-cutting: the eSIM "aliases" are (or aren't) where we expect -----

    [Fact]
    public void The_esim_alias_codes_land_in_their_true_iso_layers()
    {
        var od = Iso3166();
        Assert.Equal("CY", od.Countries.FindByAlpha3("CYP")?.Alpha2); // CYP is Cyprus' alpha-3
        Assert.NotNull(od.CountrySubdivisions.Find("US-HI")); // US-HI is a subdivision
        Assert.NotNull(od.FormerCountries.FindByAlpha2("AN")); // AN is a withdrawn code
        Assert.Null(od.Countries.Find("IC")); // IC: only exceptionally reserved
        Assert.Null(od.Countries.Find("XK")); // XK: user-assigned, not ISO
    }

    // ----- Registration wiring -----

    [Fact]
    public void Leaf_datasets_are_the_same_instances_the_aggregate_exposes()
    {
        var provider = Provider();
        var group = provider.GetRequiredService<IIso3166>();
        Assert.Same(provider.GetRequiredService<IIso3166Countries>(), group.Countries);
        Assert.Same(provider.GetRequiredService<IIso3166CountrySubdivisions>(), group.CountrySubdivisions);
        Assert.Same(provider.GetRequiredService<IIso3166FormerCountries>(), group.FormerCountries);
    }

    [Fact]
    public void Read_only_surfaces_are_not_downcastable_to_mutable_collections()
    {
        var iso = Iso3166();
        Assert.IsNotType<Iso3166Country[]>(iso.Countries.All); // All isn't a raw array
        Assert.IsNotType<List<Iso3166CountrySubdivision>>(iso.CountrySubdivisions
            .ForCountry("US")); // not the live List
    }
}