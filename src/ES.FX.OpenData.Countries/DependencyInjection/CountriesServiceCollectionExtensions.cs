using ES.FX.OpenData.Countries;
using ES.FX.OpenData.Countries.Internal;
using ES.FX.OpenData.Countries.ISO3166;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace — DI registration extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration for the ES.FX.OpenData.Countries library.</summary>
[PublicAPI]
public static class CountriesServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <b>every dataset in the library</b>: the curated countries (<see cref="ICountriesDataset" />)
    ///     and country-subdivisions (<see cref="ICountrySubdivisionsDataset" />), plus the raw ISO 3166 datasets
    ///     they are built on — <see cref="IIso3166Countries" /> (3166-1), <see cref="IIso3166CountrySubdivisions" />
    ///     (3166-2), <see cref="IIso3166FormerCountries" /> (3166-3) and the <see cref="IIso3166" /> aggregate.
    ///     Each is an independently injectable singleton. Idempotent — safe to call more than once. To register a
    ///     single dataset instead, use the granular <c>AddCountrySubdivisions()</c> / <c>AddIso3166*()</c> methods.
    /// </summary>
    public static IServiceCollection AddCountries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIso3166(); // raw ISO 3166 parts 1/2/3 + the IIso3166 aggregate
        services.AddCountrySubdivisions(); // curated subdivisions (also ensures ISO 3166-2)
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ICountriesDataset))) return services;
        services.AddSingleton<ICountriesDataset, CountriesDataset>();
        return services;
    }
}