using ES.FX.OpenData.Countries;
using ES.FX.OpenData.Countries.ISO3166;
using ES.FX.OpenData.Romania.TerritorialUnits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.OpenData.Tests;

public class OpenDataCoreTests
{
    private static ServiceProvider BuildFull()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCountries().AddRomaniaTerritorialUnits();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Datasets_are_resolvable_directly_via_di()
    {
        using var provider = BuildFull();
        Assert.NotNull(provider.GetRequiredService<ICountriesDataset>());
        Assert.NotNull(provider.GetRequiredService<IRomanianTerritorialUnitsDataset>());
    }

    [Fact]
    public void AddCountries_registers_every_dataset_in_the_library()
    {
        // AddCountries() is the library umbrella: curated countries + subdivisions AND the raw ISO 3166 datasets.
        var services = new ServiceCollection();
        services.AddCountries();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICountriesDataset>());
        Assert.NotNull(provider.GetRequiredService<ICountrySubdivisionsDataset>());
        Assert.NotNull(provider.GetRequiredService<IIso3166Countries>());
        Assert.NotNull(provider.GetRequiredService<IIso3166CountrySubdivisions>());
        Assert.NotNull(provider.GetRequiredService<IIso3166FormerCountries>());
        Assert.NotNull(provider.GetRequiredService<IIso3166>());
    }

    [Fact]
    public void Registration_is_idempotent_and_never_double_registers()
    {
        // TryAdd's idempotency is replaced by an explicit guard; adding a dataset twice must still yield exactly
        // one instance (and, critically, must not double-parse the heavy SIRUTA resource).
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCountries().AddCountries().AddRomaniaTerritorialUnits().AddRomaniaTerritorialUnits();
        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<ICountriesDataset>());
        Assert.Single(provider.GetServices<IRomanianTerritorialUnitsDataset>());
    }

    [Fact]
    public async Task Warmup_opt_in_loads_the_dataset_without_faulting()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRomaniaTerritorialUnits(true);
        using var provider = services.BuildServiceProvider();

        foreach (var hostedService in provider.GetServices<IHostedService>())
            await hostedService.StartAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(provider.GetRequiredService<IRomanianTerritorialUnitsDataset>().Find(1026));
    }

    [Fact]
    public void Without_the_warmup_opt_in_no_hosted_service_is_registered()
    {
        var services = new ServiceCollection();
        services.AddRomaniaTerritorialUnits(); // warmup defaults to false
        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetServices<IHostedService>());
    }
}