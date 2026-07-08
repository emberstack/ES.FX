using Microsoft.Extensions.DependencyInjection;

namespace ES.FX.OpenData.Romania.AdministrativeUnits.Internal;

internal sealed class RomanianAdministrativeUnitsDatasetRegistration : IOpenDatasetRegistration
{
    public OpenDatasetInfo Info => RomanianAdministrativeUnitsDataset.DatasetInfo;

    public IOpenDataset Resolve(IServiceProvider provider) =>
        provider.GetRequiredService<IRomanianAdministrativeUnitsDataset>();
}
