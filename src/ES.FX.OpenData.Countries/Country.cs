using System.Globalization;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Countries;

/// <summary>
///     A country as defined by ISO 3166-1: numeric, alpha-2 and alpha-3 codes, the English short name, and
///     localized names keyed by culture. Identity is by code — compare on <see cref="Alpha2" /> /
///     <see cref="NumericCode" />, not by reference or structural equality.
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
    ///     dataset guarantees <c>en</c> and <c>ro</c>. Prefer <see cref="GetLocalizedName" /> for lookups.
    /// </summary>
    public required IReadOnlyDictionary<string, string> LocalizedNames { get; init; }

    /// <summary>
    ///     Returns the localized name for <paramref name="culture" />, walking the culture's parent chain
    ///     (e.g. <c>de-AT</c> → <c>de</c>) and falling back to <see cref="Name" /> when no localized name exists.
    ///     Never throws for a missing culture.
    /// </summary>
    public string GetLocalizedName(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
            if (LocalizedNames.TryGetValue(current.Name, out var name))
                return name;
        return Name;
    }
}
