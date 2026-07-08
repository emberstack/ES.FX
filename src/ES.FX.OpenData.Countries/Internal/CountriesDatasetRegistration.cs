using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Countries.Internal;

internal sealed class CountriesDatasetRegistration : IOpenDatasetRegistration
{
    public OpenDatasetInfo Info => CountriesDataset.DatasetInfo;

    public IOpenDataset Resolve(IServiceProvider provider) => provider.GetRequiredService<ICountriesDataset>();
}
