using System.Globalization;
using ES.FX.OpenData.Currencies.ISO4217;
using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Currencies.Tests;

public class CurrenciesDatasetTests
{
    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddCurrencies();
        return services.BuildServiceProvider();
    }

    private static ICurrenciesDataset Currencies() => Provider().GetRequiredService<ICurrenciesDataset>();

    [Fact]
    public void AddCurrencies_registers_the_curated_and_raw_datasets()
    {
        var provider = Provider();
        Assert.NotNull(provider.GetRequiredService<ICurrenciesDataset>());
        Assert.NotNull(provider.GetRequiredService<IIso4217Currencies>());
    }

    [Fact]
    public void AddCurrencies_is_idempotent()
    {
        var services = new ServiceCollection();
        services.AddCurrencies().AddCurrencies();
        var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<ICurrenciesDataset>());
        Assert.Single(provider.GetServices<IIso4217Currencies>());
    }

    [Fact]
    public void Curated_currencies_resolve_by_alpha3_and_numeric()
    {
        var currencies = Currencies();
        Assert.Equal(181, currencies.All.Count);

        var usd = currencies["usd"]; // case-insensitive
        Assert.Equal("USD", usd.Alpha3);
        Assert.Equal(840, usd.NumericCode);
        Assert.Equal("US Dollar", usd.Name);

        Assert.Same(usd, currencies.Find("USD"));
        Assert.Same(usd, currencies.FindByNumericCode(840));
        Assert.True(currencies.TryGet("USD", out _));
    }

    [Fact]
    public void Indexer_throws_and_lookups_tolerate_unknown_codes()
    {
        var currencies = Currencies();
        Assert.Throws<KeyNotFoundException>(() => currencies["ZZZ"]);
        Assert.Null(currencies.Find("ZZZ"));
        Assert.Null(currencies.FindByNumericCode(-1));
        Assert.False(currencies.TryGet("ZZZ", out _));
    }

    [Fact]
    public void Localized_names_guarantee_english_and_fall_back_to_it()
    {
        var ron = Currencies()["RON"];
        Assert.Equal("Romanian Leu", ron.Name);
        Assert.Equal(ron.Name, ron.LocalizedNames["en"]); // en always present, equal to the ISO name

        // The overlay ships empty, so any culture falls back to the English name (and TryGet reports the fallback).
        Assert.Equal(ron.Name, ron.GetLocalizedName(CultureInfo.GetCultureInfo("ro")));
        Assert.False(ron.TryGetLocalizedName(CultureInfo.GetCultureInfo("ro"), out var name));
        Assert.Equal(ron.Name, name);
    }

    [Fact]
    public void Generated_constants_match_the_dataset()
    {
        var currencies = Currencies();
        Assert.Equal("USD", CurrencyAlpha3Codes.USDollar);
        Assert.Equal("EUR", CurrencyAlpha3Codes.Euro);
        Assert.Equal("RON", CurrencyAlpha3Codes.RomanianLeu);
        Assert.Equal(978, CurrencyNumericCodes.Euro);

        // Every generated alpha-3 constant resolves in the dataset (constants track the data).
        Assert.NotNull(currencies.Find(CurrencyAlpha3Codes.RomanianLeu));
    }

    [Fact]
    public void CurrencyCodes_are_self_named_and_resolve_in_the_dataset()
    {
        var currencies = Currencies();
        Assert.Equal("RON", CurrencyCodes.RON);
        Assert.Equal("EUR", CurrencyCodes.EUR);
        Assert.Equal("USD", CurrencyCodes.USD);
        Assert.Equal("Romanian Leu", currencies[CurrencyCodes.RON].Name);
    }

    [Fact]
    public void CurrencyCodes_have_one_self_named_constant_per_currency()
    {
        var currencies = Currencies();
        var fields = typeof(CurrencyCodes).GetFields();

        Assert.Equal(currencies.All.Count, fields.Length); // exactly one constant per currency

        foreach (var field in fields)
        {
            var code = (string)field.GetRawConstantValue()!;
            Assert.Equal(field.Name, code); // the member name IS the code
            Assert.NotNull(currencies.Find(code)); // and it resolves in the dataset
        }

        // The code set is exactly the alpha-3 set exposed by the name-keyed CurrencyAlpha3Codes class.
        var codes = fields.Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        var alpha3 = typeof(CurrencyAlpha3Codes).GetFields()
            .Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        Assert.Equal(alpha3, codes);
    }

    [Fact]
    public void Raw_iso4217_dataset_is_standard_faithful()
    {
        var iso = Provider().GetRequiredService<IIso4217Currencies>();
        Assert.Equal(181, iso.All.Count);

        var eur = iso["EUR"];
        Assert.Equal("EUR", eur.Alpha3);
        Assert.Equal(978, eur.NumericCode);
        Assert.Equal("Euro", eur.Name);
        Assert.Same(eur, iso.FindByNumericCode(978));
        Assert.Null(iso.Find("ZZZ"));
    }
}