using Microsoft.Extensions.Hosting;

namespace ES.FX.OpenData.Romania.TerritorialUnits.Internal;

/// <summary>
///     Optional startup warmup for the SIRUTA dataset, opted into via
///     <c>AddRomaniaTerritorialUnits(warmup: true)</c>. Eagerly materializes the dataset so a corrupt embedded
///     resource surfaces at host startup instead of on the first request.
/// </summary>
internal sealed class RomanianTerritorialUnitsWarmupService(RomanianTerritorialUnitsDataset dataset)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        dataset.EnsureLoaded();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}