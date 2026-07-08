using System.Globalization;
using ES.FX.OpenData;
using ES.FX.OpenData.Countries;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Countries.Tests;

public class CountriesDatasetTests
{
    private static ICountriesDataset Dataset()
    {
        var services = new ServiceCollection();
        services.AddOpenData().AddCountries();
        return services.BuildServiceProvider().GetRequiredService<ICountriesDataset>();
    }

    [Fact]
    public void All_has_the_expected_count() => Assert.Equal(249, Dataset().All.Count);

    [Fact]
    public void Indexer_returns_country_case_insensitively()
    {
        var dataset = Dataset();
        Assert.Equal("Romania", dataset["RO"].Name);
        Assert.Equal("RO", dataset["ro"].Alpha2);
    }

    [Fact]
    public void Indexer_throws_for_unknown_code() =>
        Assert.Throws<KeyNotFoundException>(() => Dataset()["ZZ"]);

    [Fact]
    public void Find_and_TryGet_tolerate_unknown_codes()
    {
        var dataset = Dataset();
        Assert.Null(dataset.Find("ZZ"));
        Assert.False(dataset.TryGet("ZZ", out var missing));
        Assert.Null(missing);
        Assert.True(dataset.TryGet("RO", out var found));
        Assert.Equal("Romania", found!.Name);
    }

    [Fact]
    public void FindByNumericCode_resolves_iso_numeric() =>
        Assert.Equal("RO", Dataset().FindByNumericCode(642)?.Alpha2);

    [Fact]
    public void GetLocalizedName_returns_romanian_and_falls_back_to_english()
    {
        var romania = Dataset()["RO"];
        Assert.Equal("România", romania.GetLocalizedName(new CultureInfo("ro")));
        Assert.Equal("România", romania.GetLocalizedName(new CultureInfo("ro-RO"))); // parent-chain fallback
        Assert.Equal("Romania", romania.GetLocalizedName(new CultureInfo("fr")));    // no localization → Name
    }

    [Fact]
    public void LocalizedNames_support_en()
    {
        var romania = Dataset()["RO"];
        Assert.True(romania.LocalizedNames.ContainsKey("en"));
        Assert.Equal("Romania", romania.LocalizedNames["en"]);
        Assert.Equal("Romania", romania.GetLocalizedName(new CultureInfo("en")));
        Assert.Equal("Romania", romania.GetLocalizedName(new CultureInfo("en-US"))); // parent-chain → en
    }

    [Fact]
    public void Every_country_and_alias_exposes_an_en_localized_name()
    {
        var dataset = Dataset();

        // Every canonical country carries an "en" entry equal to its English Name, and resolves it for
        // any English culture (directly and via the culture parent-chain, e.g. en-GB → en).
        Assert.All(dataset.All, c =>
        {
            Assert.True(c.LocalizedNames.ContainsKey("en"), $"{c.Alpha2} is missing an 'en' localized name");
            Assert.Equal(c.Name, c.LocalizedNames["en"]);
            Assert.Equal(c.Name, c.GetLocalizedName(new CultureInfo("en")));
            Assert.Equal(c.Name, c.GetLocalizedName(new CultureInfo("en-GB")));
        });

        // Alias-resolved entries (e.g. Kosovo, Northern Cyprus) expose "en" too.
        Assert.All(dataset.LookupMap.Values, c =>
        {
            Assert.True(c.LocalizedNames.ContainsKey("en"), $"alias {c.Alpha2} is missing an 'en' localized name");
            Assert.Equal(c.Name, c.LocalizedNames["en"]);
            Assert.Equal(c.Name, c.GetLocalizedName(new CultureInfo("en")));
        });
    }

    [Theory]
    [InlineData("SX", 534, "SXM", "Sint Maarten (Dutch part)")] // completed the Netherlands-Antilles trio
    [InlineData("EH", 732, "ESH", "Western Sahara")]
    [InlineData("NC", 540, "NCL", "New Caledonia")]
    [InlineData("AS", 16, "ASM", "American Samoa")]
    public void Added_m49_territories_are_present(string alpha2, int numeric, string alpha3, string name)
    {
        var dataset = Dataset();
        var country = dataset[alpha2];
        Assert.Equal(numeric, country.NumericCode);
        Assert.Equal(alpha3, country.Alpha3);
        Assert.Equal(name, country.Name);
        Assert.Same(country, dataset.FindByNumericCode(numeric));
    }

    [Fact]
    public void English_names_use_iso_diacritics()
    {
        var dataset = Dataset();
        Assert.Equal("Åland Islands", dataset["AX"].Name);
        Assert.Equal("Curaçao", dataset["CW"].Name);
        Assert.Equal("Réunion", dataset["RE"].Name);
        Assert.Equal("Türkiye", dataset["TR"].Name);
    }

    [Fact]
    public void Kosovo_is_an_alias_not_a_canonical_country()
    {
        var dataset = Dataset();

        // Kosovo (XK/XKX/383) is not in ISO 3166-1 or UN M49 → it lives only in the alias map.
        Assert.Null(dataset.Find("XK"));
        Assert.Null(dataset.FindByNumericCode(383));

        var kosovo = dataset.Resolve("XK");
        Assert.NotNull(kosovo);
        Assert.Equal("Kosovo", kosovo!.Name);
        Assert.Equal("XK", kosovo.Alpha2);
    }

    [Fact]
    public void Resolve_accepts_alias_codes_but_canonical_list_stays_clean()
    {
        var dataset = Dataset();

        var northernCyprus = dataset.Resolve("CYP");
        Assert.NotNull(northernCyprus);
        Assert.Equal("Northern Cyprus", northernCyprus!.Name);
        Assert.Equal("CY", northernCyprus.Alpha2); // canonical identity preserved

        Assert.Null(dataset.Find("CYP"));           // alias not promoted into the canonical list
        Assert.True(dataset.LookupMap.ContainsKey("CYP"));
    }

    [Fact]
    public void Info_describes_the_dataset()
    {
        var info = Dataset().Info;
        Assert.Equal("Countries", info.Name);
        Assert.Equal("ISO 3166-1", info.Standard);
    }
}
