using ES.FX.OpenData.Countries.ISO3166;
using ES.FX.OpenData.Countries.ISO3166.Internal;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable once CheckNamespace — DI registration extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration for the ISO 3166 reference datasets (parts 1, 2 and 3).</summary>
[PublicAPI]
public static class Iso3166ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers all three ISO 3166 datasets — <see cref="IIso3166Countries" /> (3166-1),
    ///     <see cref="IIso3166CountrySubdivisions" /> (3166-2) and <see cref="IIso3166FormerCountries" />
    ///     (3166-3) — plus the <see cref="IIso3166" /> aggregate for grouped access. Each leaf is an
    ///     independently injectable singleton. Idempotent — safe to call more than once.
    /// </summary>
    public static IServiceCollection AddIso3166(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIso3166Countries();
        services.AddIso3166CountrySubdivisions();
        services.AddIso3166FormerCountries();
        services.TryAddSingleton<IIso3166, Iso3166Accessor>();
        return services;
    }

    /// <summary>Registers only the ISO 3166-1 country-code dataset (<see cref="IIso3166Countries" />). Idempotent.</summary>
    public static IServiceCollection AddIso3166Countries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IIso3166Countries))) return services;
        services.AddSingleton<IIso3166Countries, Iso3166CountriesDataset>();
        return services;
    }

    /// <summary>
    ///     Registers only the ISO 3166-2 subdivision-code dataset (<see cref="IIso3166CountrySubdivisions" />).
    ///     Idempotent.
    /// </summary>
    public static IServiceCollection AddIso3166CountrySubdivisions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IIso3166CountrySubdivisions))) return services;
        services.AddSingleton<IIso3166CountrySubdivisions, Iso3166CountrySubdivisionsDataset>();
        return services;
    }

    /// <summary>
    ///     Registers only the ISO 3166-3 formerly-used-country-code dataset (<see cref="IIso3166FormerCountries" />).
    ///     Idempotent.
    /// </summary>
    public static IServiceCollection AddIso3166FormerCountries(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IIso3166FormerCountries))) return services;
        services.AddSingleton<IIso3166FormerCountries, Iso3166FormerCountriesDataset>();
        return services;
    }
}