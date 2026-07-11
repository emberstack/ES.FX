using ES.FX.OpenData.Currencies;
using ES.FX.OpenData.Currencies.Internal;
using ES.FX.OpenData.Currencies.ISO4217;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace — DI registration extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration for the ES.FX.OpenData.Currencies library.</summary>
[PublicAPI]
public static class CurrenciesServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <b>every dataset in the library</b>: the curated currencies
    ///     (<see cref="ICurrenciesDataset" />) and the raw ISO 4217 dataset it is built on
    ///     (<see cref="IIso4217Currencies" />). Each is an independently injectable singleton. Idempotent — safe to
    ///     call more than once. To register only the raw ISO 4217 dataset, use <c>AddIso4217()</c>.
    /// </summary>
    public static IServiceCollection AddCurrencies(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddIso4217();
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ICurrenciesDataset))) return services;
        services.AddSingleton<ICurrenciesDataset, CurrenciesDataset>();
        return services;
    }
}