using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries.ISO3166;

/// <summary>
///     Aggregate grouping the three ISO 3166 datasets under one injectable type (registered by
///     <c>AddIso3166()</c>). Each leaf is an independently registered dataset that can also be injected directly
///     (e.g. inject <see cref="IIso3166Countries" />).
/// </summary>
[PublicAPI]
public interface IIso3166
{
    /// <summary>The ISO 3166-1 country codes (leaf dataset).</summary>
    IIso3166Countries Countries { get; }

    /// <summary>The ISO 3166-2 subdivision codes (leaf dataset).</summary>
    IIso3166CountrySubdivisions CountrySubdivisions { get; }

    /// <summary>The ISO 3166-3 formerly used country codes (leaf dataset).</summary>
    IIso3166FormerCountries FormerCountries { get; }
}