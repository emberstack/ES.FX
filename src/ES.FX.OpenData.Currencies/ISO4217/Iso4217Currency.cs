using JetBrains.Annotations;

namespace ES.FX.OpenData.Currencies.ISO4217;

/// <summary>
///     A currency as defined by <b>ISO 4217</b>: the alpha-3 code, numeric code, and English name. This is the
///     standard-faithful ISO 4217 record — for a curated, localization-friendly currency list use
///     <c>ES.FX.OpenData.Currencies</c> instead. Identity is by code — compare on <see cref="Alpha3" /> /
///     <see cref="NumericCode" />, not by reference.
/// </summary>
[PublicAPI]
public sealed class Iso4217Currency
{
    /// <summary>ISO 4217 alpha-3 code (e.g. <c>"USD"</c>).</summary>
    public required string Alpha3 { get; init; }

    /// <summary>ISO 4217 numeric code (e.g. <c>840</c>).</summary>
    public required int NumericCode { get; init; }

    /// <summary>The English currency name (e.g. <c>"US Dollar"</c>).</summary>
    public required string Name { get; init; }
}