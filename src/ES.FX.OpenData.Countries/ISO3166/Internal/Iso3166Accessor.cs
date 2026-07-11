namespace ES.FX.OpenData.Countries.ISO3166.Internal;

/// <summary>
///     The <see cref="IIso3166" /> group aggregate. A thin facade over the three independently-registered leaf
///     datasets (each a singleton), injected directly.
/// </summary>
internal sealed class Iso3166Accessor(
    IIso3166Countries countries,
    IIso3166CountrySubdivisions countrySubdivisions,
    IIso3166FormerCountries formerCountries) : IIso3166
{
    public IIso3166Countries Countries => countries;
    public IIso3166CountrySubdivisions CountrySubdivisions => countrySubdivisions;
    public IIso3166FormerCountries FormerCountries => formerCountries;
}