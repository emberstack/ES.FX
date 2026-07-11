using ES.FX.OpenData.Romania.TerritorialUnits;
using ES.FX.OpenData.Romania.TerritorialUnits.Internal;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace — DI registration extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration for the Romanian SIRUTA administrative-units dataset.</summary>
[PublicAPI]
public static class RomaniaTerritorialUnitsServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the SIRUTA dataset (<see cref="IRomanianTerritorialUnitsDataset" />). County ISO 3166-2
    ///     codes and names are sourced from the ISO 3166-2 dataset — this method registers it via
    ///     <see cref="Iso3166ServiceCollectionExtensions.AddIso3166CountrySubdivisions" />. Idempotent — safe to
    ///     call more than once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="warmup">
    ///     When <c>true</c>, registers a hosted service that eagerly materializes the dataset at host startup, so a
    ///     corrupt embedded resource surfaces at boot rather than as a cached fault on first access. SIRUTA parses
    ///     ~17k rows, so this trades a little startup time for first-request latency. Defaults to <c>false</c>
    ///     (lazy on first access).
    /// </param>
    public static IServiceCollection AddRomaniaTerritorialUnits(this IServiceCollection services,
        bool warmup = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIso3166CountrySubdivisions();
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IRomanianTerritorialUnitsDataset)))
        {
            services.AddSingleton<RomanianTerritorialUnitsDataset>();
            services.AddSingleton<IRomanianTerritorialUnitsDataset>(provider =>
                provider.GetRequiredService<RomanianTerritorialUnitsDataset>());
        }

        if (warmup) services.AddHostedService<RomanianTerritorialUnitsWarmupService>();
        return services;
    }
}