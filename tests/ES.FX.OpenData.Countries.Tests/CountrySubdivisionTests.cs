using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Countries.Tests;

public class CountrySubdivisionTests
{
    private static ICountrySubdivisionsDataset Dataset()
    {
        var services = new ServiceCollection();
        services.AddCountrySubdivisions();
        return services.BuildServiceProvider().GetRequiredService<ICountrySubdivisionsDataset>();
    }

    [Fact]
    public void ForCountry_returns_the_countrys_subdivisions()
    {
        var usStates = Dataset().ForCountry("US");
        Assert.NotEmpty(usStates);
        Assert.Contains(usStates, s => s.Code == "US-HI");
        Assert.All(usStates, s => Assert.Equal("US", s.CountryAlpha2));
    }

    [Fact]
    public void ForCountry_is_case_insensitive_and_empty_for_unknown()
    {
        var dataset = Dataset();
        Assert.NotEmpty(dataset.ForCountry("us")); // case-insensitive
        Assert.Empty(dataset.ForCountry("ZZ")); // unknown country → empty, not throw
    }

    [Fact]
    public void Indexer_and_find_round_trip_a_code()
    {
        var dataset = Dataset();
        var hawaii = dataset["US-HI"];
        Assert.Equal("Hawaii", hawaii.Name);
        Assert.Equal("State", hawaii.Type);
        Assert.Equal("US", hawaii.CountryAlpha2);
        Assert.Null(hawaii.Parent);
        Assert.Same(hawaii, dataset.Find("us-hi")); // case-insensitive, same singleton instance
    }

    [Fact]
    public void Indexer_throws_and_find_tolerates_unknown_codes()
    {
        var dataset = Dataset();
        Assert.Throws<KeyNotFoundException>(() => dataset["US-ZZ"]);
        Assert.Null(dataset.Find("US-ZZ"));
        Assert.False(dataset.TryGet("US-ZZ", out var missing));
        Assert.Null(missing);
        Assert.True(dataset.TryGet("US-HI", out var found));
        Assert.Equal("Hawaii", found!.Name);
    }

    [Fact]
    public void Subdivision_guarantees_en_and_falls_back_for_untranslated_cultures()
    {
        var hawaii = Dataset()["US-HI"];
        Assert.Equal("Hawaii", hawaii.LocalizedNames["en"]); // en guaranteed (= ISO name)
        Assert.Equal("Hawaii", hawaii.GetLocalizedName(new CultureInfo("en")));
        Assert.Equal("Hawaii",
            hawaii.GetLocalizedName(new CultureInfo("ro"))); // no ro overlay yet → falls back to Name
    }

    [Fact]
    public void Nested_subdivisions_carry_their_parent()
    {
        // Azerbaijani rayons nest under the Nakhchivan autonomous republic (AZ-NX).
        var babek = Dataset()["AZ-BAB"];
        Assert.Equal("AZ-NX", babek.Parent);
        Assert.Equal("AZ", babek.CountryAlpha2);
    }

    [Fact]
    public void Read_only_surfaces_are_not_downcastable_to_mutable_collections()
    {
        var dataset = Dataset();
        Assert.IsNotType<CountrySubdivision[]>(dataset.All);
        Assert.IsNotType<CountrySubdivision[]>(dataset.ForCountry("US"));
        Assert.IsNotType<Dictionary<string, string>>(dataset["US-HI"].LocalizedNames);
    }
}