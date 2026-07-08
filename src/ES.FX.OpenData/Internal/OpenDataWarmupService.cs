using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ES.FX.OpenData.Internal;

/// <summary>
///     Warms every registered dataset according to <see cref="OpenDataOptions.WarmupMode" />, so a corrupt or
///     missing resource surfaces as a loud startup log rather than a caching <see cref="Lazy{T}" /> exception on
///     the first user request. Per-dataset failures are caught and logged, never fired-and-forgotten unobserved.
/// </summary>
internal sealed class OpenDataWarmupService(
    IEnumerable<IOpenDatasetRegistration> registrations,
    IServiceProvider provider,
    OpenDataOptions options,
    ILogger<OpenDataWarmupService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        switch (options.WarmupMode)
        {
            case OpenDataWarmupMode.None:
                return Task.CompletedTask;
            case OpenDataWarmupMode.Blocking:
                WarmAll();
                return Task.CompletedTask;
            case OpenDataWarmupMode.Background:
            default:
                _ = Task.Run(WarmAll, cancellationToken);
                return Task.CompletedTask;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void WarmAll()
    {
        foreach (var registration in registrations)
            try
            {
                var dataset = registration.Resolve(provider);
                var stopwatch = Stopwatch.StartNew();
                dataset.EnsureLoaded();
                stopwatch.Stop();
                logger.LogInformation(
                    "OpenData dataset {Dataset} (edition {Edition}) loaded in {ElapsedMs}ms",
                    registration.Info.Name, registration.Info.Edition, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "OpenData dataset {Dataset} (edition {Edition}) failed to load",
                    registration.Info.Name, registration.Info.Edition);
            }
    }
}
