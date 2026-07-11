using ES.FX.OpenData.Currencies.ISO4217;
using ES.FX.OpenData.Currencies.ISO4217.Internal;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace — DI registration extensions are conventionally in this namespace.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration for the ISO 4217 currency-code dataset.</summary>
[PublicAPI]
public static class Iso4217ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the ISO 4217 currency-code dataset (<see cref="IIso4217Currencies" />) as an independently
    ///     injectable singleton. Idempotent — safe to call more than once.
    /// </summary>
    public static IServiceCollection AddIso4217(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IIso4217Currencies))) return services;
        services.AddSingleton<IIso4217Currencies, Iso4217CurrenciesDataset>();
        return services;
    }
}