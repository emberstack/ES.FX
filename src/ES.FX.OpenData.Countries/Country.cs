using System.Globalization;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries;

/// <summary>
///     A country as defined by ISO 3166-1: numeric, alpha-2 and alpha-3 codes, the English short name, and
///     localized names keyed by culture. Identity is by code — compare on <see cref="Alpha2" /> /
///     <see cref="NumericCode" />, not by reference or structural equality. The regional-indicator flag emoji is
///     derivable from <see cref="Alpha2" />; the long-form official name lives on
///     <c>ES.FX.OpenData.Countries.ISO3166.Iso3166Country</c> if you need it.
/// </summary>
[PublicAPI]
public sealed class Country
{
    /// <summary>ISO 3166-1 alpha-2 code (e.g. <c>"RO"</c>).</summary>
    public required string Alpha2 { get; init; }

    /// <summary>ISO 3166-1 alpha-3 code (e.g. <c>"ROU"</c>).</summary>
    public required string Alpha3 { get; init; }

    /// <summary>ISO 3166-1 numeric code (e.g. <c>642</c>).</summary>
    public required int NumericCode { get; init; }

    /// <summary>The English short name (e.g. <c>"Romania"</c>). The least-surprise default for display.</summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Localized names keyed by culture name (case-insensitive), e.g. <c>["ro"] = "România"</c>. The base
    ///     dataset guarantees <c>en</c> and <c>ro</c>. Prefer <see cref="GetLocalizedName(CultureInfo)" /> for lookups.
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
    ///     fallback, which <see cref="GetLocalizedName(CultureInfo)" /> cannot.
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