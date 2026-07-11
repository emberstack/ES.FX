using ES.FX.OpenData.Countries;
using ES.FX.OpenData.Countries.Internal;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace — DI registration extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration for the curated country-subdivisions dataset.</summary>
[PublicAPI]
public static class CountrySubdivisionsServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the curated country-subdivisions dataset (<see cref="ICountrySubdivisionsDataset" />).
    ///     Subdivision identity (code, country prefix, ISO name, type, parent) is sourced from the ISO 3166-2
    ///     dataset — this method registers it via
    ///     <see cref="Iso3166ServiceCollectionExtensions.AddIso3166CountrySubdivisions" /> — and layered with
    ///     localized names. Registers only this dataset; <c>AddCountries()</c> also includes it. Idempotent.
    /// </summary>
    public static IServiceCollection AddCountrySubdivisions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIso3166CountrySubdivisions();
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ICountrySubdivisionsDataset))) return services;
        services.AddSingleton<ICountrySubdivisionsDataset, CountrySubdivisionsDataset>();
        return services;
    }
}