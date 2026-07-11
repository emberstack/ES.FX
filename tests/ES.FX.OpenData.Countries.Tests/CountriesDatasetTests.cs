using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ES.FX.OpenData.Countries.ISO3166;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Countries.Tests;

public class CountriesDatasetTests
{
    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddCountries();
        return services.BuildServiceProvider();
    }

    private static ICountriesDataset Dataset() => Provider().GetRequiredService<ICountriesDataset>();

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
    public void FindByAlpha3_resolves_iso_alpha3_case_insensitively_and_tolerates_unknown()
    {
        var dataset = Dataset();
        Assert.Equal("RO", dataset.FindByAlpha3("ROU")?.Alpha2);
        Assert.Equal("RO", dataset.FindByAlpha3("rou")?.Alpha2); // case-insensitive
        Assert.Null(dataset.FindByAlpha3("ZZZ"));
    }

    [Fact]
    public void Read_only_surfaces_are_not_downcastable_to_mutable_collections()
    {
        var dataset = Dataset();
        Assert.IsNotType<Country[]>(dataset.All); // All isn't a raw array
        Assert.IsNotType<Dictionary<string, string>>(dataset["RO"].LocalizedNames); // not a mutable dictionary
    }

    [Fact]
    public void GetLocalizedName_returns_romanian_and_falls_back_to_english()
    {
        var romania = Dataset()["RO"];
        Assert.Equal("România", romania.GetLocalizedName(new CultureInfo("ro")));
        Assert.Equal("România", romania.GetLocalizedName(new CultureInfo("ro-RO"))); // parent-chain fallback
        Assert.Equal("Romania", romania.GetLocalizedName(new CultureInfo("fr"))); // no localization → Name
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
    public void Every_country_exposes_an_en_localized_name()
    {
        // Every country carries an "en" entry equal to its English Name, and resolves it for any English
        // culture (directly and via the culture parent-chain, e.g. en-GB → en).
        Assert.All(Dataset().All, c =>
        {
            Assert.True(c.LocalizedNames.ContainsKey("en"), $"{c.Alpha2} is missing an 'en' localized name");
            Assert.Equal(c.Name, c.LocalizedNames["en"]);
            Assert.Equal(c.Name, c.GetLocalizedName(new CultureInfo("en")));
            Assert.Equal(c.Name, c.GetLocalizedName(new CultureInfo("en-GB")));
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
    public void Non_iso_codes_are_not_present()
    {
        // Non-ISO codes — an alpha-3 (CYP), a subdivision (US-HI), reserved (IC), a withdrawn code (AN), and
        // the user-assigned XK/Kosovo — are not part of the ISO 3166-1 alpha-2 set this dataset serves.
        var dataset = Dataset();
        foreach (var code in new[] { "CYP", "US-HI", "IC", "AN", "XK" })
            Assert.Null(dataset.Find(code));
    }

    // ----- Single source of truth: identity comes from ES.FX.OpenData.Countries.ISO3166 -----

    [Fact]
    public void Identity_is_sourced_from_the_iso3166_dataset()
    {
        var provider = Provider();
        var countries = provider.GetRequiredService<ICountriesDataset>();
        var iso = provider.GetRequiredService<IIso3166Countries>();

        Assert.Equal(iso.All.Count, countries.All.Count); // no duplicate/divergent country list
        Assert.All(countries.All, c =>
        {
            var source = iso[c.Alpha2];
            Assert.Equal(source.Alpha3, c.Alpha3);
            Assert.Equal(source.NumericCode, c.NumericCode);
            Assert.Equal(source.CommonName ?? source.Name, c.Name); // common_name preferred for display
        });
    }

    [Theory]
    [InlineData("KR", "South Korea")] // formal "Korea, Republic of" → common_name
    [InlineData("US", "United States")]
    [InlineData("IR", "Iran")]
    [InlineData("VN", "Vietnam")]
    [InlineData("BO", "Bolivia")]
    public void Common_name_is_preferred_for_the_english_display_name(string alpha2, string name) =>
        Assert.Equal(name, Dataset()[alpha2].Name);

    // ----- Localized names (curated Romanian, verified against Romanian Wikipedia + Unicode CLDR) -----

    [Theory]
    [InlineData("RO", "România")]
    [InlineData("DE", "Germania")]
    [InlineData("FR", "Franța")]
    [InlineData("AF", "Afganistan")]
    public void Romanian_names_are_localized(string alpha2, string romanian) =>
        Assert.Equal(romanian, Dataset()[alpha2].GetLocalizedName(new CultureInfo("ro")));

    [Fact]
    public void Every_country_has_a_romanian_name()
    {
        // Full ro coverage: curated Romanian names (most match English; a handful are true Romanian exonyms).
        Assert.All(Dataset().All, c =>
        {
            Assert.True(c.LocalizedNames.ContainsKey("ro"), $"{c.Alpha2} is missing a Romanian name");
            Assert.False(string.IsNullOrWhiteSpace(c.GetLocalizedName(new CultureInfo("ro"))));
        });
    }

    [Theory]
    [InlineData("AZ", "Azerbaidjan")]
    [InlineData("GN", "Guineea")]
    [InlineData("GW", "Guineea-Bissau")]
    [InlineData("KM", "Comore")]
    [InlineData("KZ", "Kazahstan")]
    [InlineData("TJ", "Tadjikistan")]
    public void Curated_romanian_exonyms_are_applied(string alpha2, string romanian) =>
        Assert.Equal(romanian, Dataset()[alpha2].GetLocalizedName(new CultureInfo("ro")));

    [Theory]
    [InlineData("KR", "Coreea de Sud")] // common form, not the formal "Republica Coreea"
    [InlineData("KP", "Coreea de Nord")]
    [InlineData("IR", "Iran")] // was "Iran, Republica islamică"
    [InlineData("MD", "Republica Moldova")]
    [InlineData("ZA", "Africa de Sud")] // capitalization
    [InlineData("TW", "Taiwan")]
    [InlineData("SX", "Sint Maarten (partea olandeză)")]
    public void Reconciled_romanian_names_use_the_most_common_form(string alpha2, string romanian) =>
        Assert.Equal(romanian, Dataset()[alpha2].GetLocalizedName(new CultureInfo("ro")));

    [Theory]
    [InlineData("NL", "Țările de Jos")] // official (MAE/EU/CLDR) — supersedes "Olanda"
    [InlineData("CI", "Côte d'Ivoire")] // official untranslated endonym
    [InlineData("TL", "Timor-Leste")] // official untranslated endonym
    [InlineData("ST", "São Tomé și Príncipe")] // proper diacritics
    [InlineData("BM", "Bermuda")]
    public void Official_latest_romanian_names_are_used(string alpha2, string romanian) =>
        Assert.Equal(romanian, Dataset()[alpha2].GetLocalizedName(new CultureInfo("ro")));

    [Fact]
    public void No_romanian_name_keeps_a_formal_inverted_form()
    {
        // Formal inverted forms ("Iran, Republica islamică", "Moldova, Republica", "Taiwan, Provincie
        // chineză") were reconciled to the common Romanian names — none should survive.
        Assert.DoesNotContain(Dataset().All, c =>
        {
            var ro = c.GetLocalizedName(new CultureInfo("ro"));
            return ro.Contains(", Republica") || ro.Contains(", Statele") || ro.Contains(", Provincie");
        });
    }

    // ----- Generated CountryAlpha2Codes / CountryAlpha3Codes constants -----

    [Fact]
    public void Country_code_constants_resolve_in_the_dataset()
    {
        var dataset = Dataset();
        Assert.Equal("RO", CountryAlpha2Codes.Romania);
        Assert.Equal("ROU", CountryAlpha3Codes.Romania);
        Assert.Equal(642, CountryNumericCodes.Romania);
        Assert.Equal("United States", dataset[CountryAlpha2Codes.UnitedStates].Name);
        Assert.Equal("South Korea", dataset[CountryAlpha2Codes.SouthKorea].Name); // common_name-based identifier
    }

    [Fact]
    public void Country_code_constants_cover_every_country_and_match_the_dataset()
    {
        var dataset = Dataset();
        var alpha2 = typeof(CountryAlpha2Codes).GetFields();
        var alpha3 = typeof(CountryAlpha3Codes).GetFields();
        var numeric = typeof(CountryNumericCodes).GetFields();

        Assert.Equal(dataset.All.Count, alpha2.Length); // exactly one constant per country…
        Assert.Equal(alpha2.Length, alpha3.Length); // …in each class
        Assert.Equal(alpha2.Length, numeric.Length);

        foreach (var field in alpha2)
        {
            var code = (string)field.GetRawConstantValue()!;
            var country = dataset.Find(code);
            Assert.NotNull(country); // alpha-2 constant resolves
            var alpha3Field = typeof(CountryAlpha3Codes).GetField(field.Name);
            Assert.NotNull(alpha3Field); // same member exists in alpha-3
            Assert.Equal(country!.Alpha3, (string)alpha3Field!.GetRawConstantValue()!); // and matches the dataset
            var numericField = typeof(CountryNumericCodes).GetField(field.Name);
            Assert.NotNull(numericField); // …and in numeric
            Assert.Equal(country.NumericCode, (int)numericField!.GetRawConstantValue()!);
        }
    }

    [Fact]
    public void Generated_member_names_derive_from_the_dataset_display_name()
    {
        // Drift guard: CountryCodes.tt re-implements the "common_name ?? name" display rule independently of
        // CountriesStore. Each generated member name must PascalCase from the resolved Country.Name (or
        // Name + alpha-2 on a collision), so a change to one name rule that isn't mirrored is caught here.
        var dataset = Dataset();
        foreach (var field in typeof(CountryAlpha2Codes).GetFields())
        {
            var alpha2 = (string)field.GetRawConstantValue()!;
            var country = dataset[alpha2];
            var id = MemberIdentifier(country.Name);
            Assert.True(field.Name == id || field.Name == id + country.Alpha2,
                $"Member '{field.Name}' ({alpha2}) does not derive from Name '{country.Name}' " +
                $"(expected '{id}' or '{id + country.Alpha2}').");
        }
    }

    // ----- Generated CountryCodes constants (keyed by the alpha-2 code itself) -----

    [Fact]
    public void CountryCodes_are_self_named_and_resolve_in_the_dataset()
    {
        var dataset = Dataset();
        Assert.Equal("RO", CountryCodes.RO);
        Assert.Equal("US", CountryCodes.US);
        Assert.Equal("Romania", dataset[CountryCodes.RO].Name);
    }

    [Fact]
    public void CountryCodes_have_one_self_named_constant_per_country()
    {
        var dataset = Dataset();
        var fields = typeof(CountryCodes).GetFields();

        Assert.Equal(dataset.All.Count, fields.Length); // exactly one constant per country

        foreach (var field in fields)
        {
            var code = (string)field.GetRawConstantValue()!;
            Assert.Equal(field.Name, code); // the member name IS the code
            Assert.NotNull(dataset.Find(code)); // and it resolves in the dataset
        }

        // The code set is exactly the alpha-2 set exposed by the name-keyed CountryAlpha2Codes class.
        var codes = fields.Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        var alpha2 = typeof(CountryAlpha2Codes).GetFields()
            .Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        Assert.Equal(alpha2, codes);
    }

    [Fact]
    public void TryGetLocalizedName_distinguishes_a_translation_from_the_english_fallback()
    {
        var romania = Dataset()["RO"];
        Assert.True(romania.TryGetLocalizedName(new CultureInfo("ro"), out var ro));
        Assert.Equal("România", ro);
        Assert.False(romania.TryGetLocalizedName(new CultureInfo("fr"), out var fr)); // no fr → fallback
        Assert.Equal("Romania", fr); // == English Name
        Assert.Equal("România", romania.GetLocalizedName("ro")); // string-culture overload
    }

    // Mirror of the PascalCase identifier rule in CountryCodes.tt (diacritics stripped, non-alnum removed).
    private static string MemberIdentifier(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        var ascii = sb.ToString().Normalize(NormalizationForm.FormC);
        var parts = Regex.Split(ascii, "[^A-Za-z0-9]+").Where(p => p.Length > 0);
        var id = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        if (id.Length == 0) id = "_";
        if (char.IsDigit(id[0])) id = "_" + id;
        return id;
    }
}