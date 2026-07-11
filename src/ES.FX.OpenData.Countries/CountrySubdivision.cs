using System.Globalization;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries;

/// <summary>
///     A curated country subdivision (state, province, region, județ, …) as defined by ISO 3166-2, with
///     localized names keyed by culture — the subdivision counterpart to <see cref="Country" />. Identity is by
///     <see cref="Code" /> — compare on it, not by reference or structural equality.
/// </summary>
[PublicAPI]
public sealed class CountrySubdivision
{
    /// <summary>The full ISO 3166-2 subdivision code (e.g. <c>"US-HI"</c>, <c>"RO-CJ"</c>).</summary>
    public required string Code { get; init; }

    /// <summary>The ISO 3166-1 alpha-2 code of the country this subdivision belongs to (e.g. <c>"US"</c>).</summary>
    public required string CountryAlpha2 { get; init; }

    /// <summary>The English/ISO-recorded short name (e.g. <c>"Hawaii"</c>). The least-surprise default for display.</summary>
    public required string Name { get; init; }

    /// <summary>The subdivision category (e.g. <c>"State"</c>, <c>"Municipality"</c>, <c>"Region"</c>).</summary>
    public required string Type { get; init; }

    /// <summary>
    ///     The parent subdivision's <see cref="Code" /> when this subdivision is nested (e.g. an Azerbaijani
    ///     rayon whose parent is an autonomous republic); otherwise <c>null</c>.
    /// </summary>
    public string? Parent { get; init; }

    /// <summary>
    ///     Localized names keyed by culture name (case-insensitive), e.g. <c>["ro"] = "Cluj"</c>. The base dataset
    ///     guarantees <c>en</c> (equal to <see cref="Name" />); other cultures are present only where a curated
    ///     translation exists. Prefer <see cref="GetLocalizedName(CultureInfo)" /> for lookups.
    /// </summary>
    public required IReadOnlyDictionary<string, string> LocalizedNames { get; init; }

    /// <summary>
    ///     Returns the localized name for <paramref name="culture" />, walking the culture's parent chain
    ///     (e.g. <c>de-AT</c> → <c>de</c>) and falling back to <see cref="Name" /> when no localized name exists.
    ///     Never throws for a missing culture. Use <see cref="TryGetLocalizedName" /> to distinguish a real
    ///     translation from the fallback.
    /// </summary>
    public string GetLocalizedName(CultureInfo culture)
    {
        TryGetLocalizedName(culture, out var name);
        return name;
    }

    /// <summary>
    ///     Returns the localized name for the given culture name (e.g. <c>"ro"</c>, <c>"de-AT"</c>), with the same
    ///     parent-chain walk and <see cref="Name" /> fallback as <see cref="GetLocalizedName(CultureInfo)" />.
    /// </summary>
    /// <exception cref="CultureNotFoundException"><paramref name="culture" /> is not a valid culture name.</exception>
    public string GetLocalizedName(string culture) => GetLocalizedName(CultureInfo.GetCultureInfo(culture));

    /// <summary>
    ///     Tries to get a genuinely localized name for <paramref name="culture" /> (walking its parent chain).
    ///     Returns <c>true</c> with the translation when one exists, or <c>false</c> with the English
    ///     <see cref="Name" /> fallback when none does — so callers can tell a real translation apart from the
    ///     fallback, which <see cref="GetLocalizedName(CultureInfo)" /> cannot. (Only <c>en</c> is curated today,
    ///     so this returns <c>false</c> for every other culture until translations are added.)
    /// </summary>
    public bool TryGetLocalizedName(CultureInfo culture, out string name)
    {
        ArgumentNullException.ThrowIfNull(culture);
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
            if (LocalizedNames.TryGetValue(current.Name, out var localized))
            {
                name = localized;
                return true;
            }

        name = Name;
        return false;
    }
}