using ES.FX.OpenData;
using ES.FX.OpenData.Countries;
using ES.FX.OpenData.Romania.AdministrativeUnits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ES.FX.OpenData.Tests;

public class OpenDataCoreTests
{
    private static ServiceProvider BuildFull(Action<OpenDataOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenData(configure).AddCountries().AddRomaniaAdministrativeUnits();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Datasets_are_resolvable_directly_via_di()
    {
        using var provider = BuildFull();
        Assert.NotNull(provider.GetRequiredService<ICountriesDataset>());
        Assert.NotNull(provider.GetRequiredService<IRomanianAdministrativeUnitsDataset>());
    }

    [Fact]
    public void Datasets_diagnostics_lists_every_registered_dataset()
    {
        // Guards against the TryAddEnumerable-of-instances de-dup hazard: each package must contribute a
        // distinct registration implementation type so all of them survive.
        using var provider = BuildFull();
        var openData = provider.GetRequiredService<IOpenData>();

        Assert.Equal(2, openData.Datasets.Count);
        Assert.Contains(openData.Datasets, d => d.Name == "Countries");
        Assert.Contains(openData.Datasets, d => d.Name == "SIRUTA");
    }

    [Fact]
    public void Hub_extension_properties_resolve_the_same_singletons()
    {
        using var provider = BuildFull();
        var openData = provider.GetRequiredService<IOpenData>();

        Assert.Same(provider.GetRequiredService<ICountriesDataset>(), openData.Countries);
        Assert.Same(provider.GetRequiredService<IRomanianAdministrativeUnitsDataset>(),
            openData.RomaniaAdministrativeUnits);
    }

    [Fact]
    public void Hub_throws_actionable_error_for_unregistered_dataset()
    {
        var services = new ServiceCollection();
        services.AddOpenData(); // no datasets added
        using var provider = services.BuildServiceProvider();
        var openData = provider.GetRequiredService<IOpenData>();

        var exception = Assert.Throws<InvalidOperationException>(() => openData.GetDataset<ICountriesDataset>());
        Assert.Contains("AddCountries", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blocking_warmup_loads_every_dataset_without_faulting()
    {
        using var provider = BuildFull(o => o.WarmupMode = OpenDataWarmupMode.Blocking);

        foreach (var hostedService in provider.GetServices<IHostedService>())
            await hostedService.StartAsync(CancellationToken.None);

        // Datasets are usable after warmup.
        Assert.Equal("Romania", provider.GetRequiredService<ICountriesDataset>()["RO"].Name);
        Assert.NotNull(provider.GetRequiredService<IRomanianAdministrativeUnitsDataset>().Find(1026));
    }
}
