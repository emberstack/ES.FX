using ES.FX.OpenData.Romania.AdministrativeUnits;
using ES.FX.OpenData.Romania.AdministrativeUnits.Internal;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Declared in the family namespace so a consumer's single `using ES.FX.OpenData;` surfaces both the
// registration method and the fluent hub accessor.
namespace ES.FX.OpenData;

/// <summary>Registration and fluent-hub access for the Romanian SIRUTA administrative-units dataset.</summary>
[PublicAPI]
public static class RomaniaAdministrativeUnitsOpenDataExtensions
{
    /// <summary>Registers the SIRUTA dataset (<see cref="IRomanianAdministrativeUnitsDataset" />).</summary>
    public static IOpenDataBuilder AddRomaniaAdministrativeUnits(this IOpenDataBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<IRomanianAdministrativeUnitsDataset, RomanianAdministrativeUnitsDataset>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOpenDatasetRegistration, RomanianAdministrativeUnitsDatasetRegistration>());
        return builder;
    }

    extension(IOpenData openData)
    {
        /// <summary>The Romanian SIRUTA dataset. Requires <see cref="AddRomaniaAdministrativeUnits" />.</summary>
        public IRomanianAdministrativeUnitsDataset RomaniaAdministrativeUnits =>
            openData.GetDataset<IRomanianAdministrativeUnitsDataset>();
    }
}
