using ES.FX.OpenData.Countries;
using ES.FX.OpenData.Countries.Internal;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Declared in the family namespace (not the package's own) so the single `using ES.FX.OpenData;` that a
// consumer already has lights up both the registration method and the fluent hub accessor.
namespace ES.FX.OpenData;

/// <summary>Registration and fluent-hub access for the ISO 3166-1 countries dataset.</summary>
[PublicAPI]
public static class CountriesOpenDataExtensions
{
    /// <summary>Registers the ISO 3166-1 countries dataset (<see cref="ICountriesDataset" />).</summary>
    public static IOpenDataBuilder AddCountries(this IOpenDataBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<ICountriesDataset, CountriesDataset>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOpenDatasetRegistration, CountriesDatasetRegistration>());
        return builder;
    }

    extension(IOpenData openData)
    {
        /// <summary>The ISO 3166-1 countries dataset. Requires <see cref="AddCountries" />.</summary>
        public ICountriesDataset Countries => openData.GetDataset<ICountriesDataset>();
    }
}
