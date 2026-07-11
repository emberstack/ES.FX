using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Currencies.ISO4217;

/// <summary>
///     Read access to the <b>ISO 4217</b> currency-code dataset. Lookups follow the family contract: the indexer
///     throws for an unknown alpha-3 code, while <see cref="Find" /> / <see cref="TryGet" /> tolerate misses.
/// </summary>
[PublicAPI]
public interface IIso4217Currencies
{
    /// <summary>All currencies, in source order.</summary>
    IReadOnlyList<Iso4217Currency> All { get; }

    /// <summary>Gets a currency by ISO 4217 alpha-3 code (case-insensitive).</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the code is not a known alpha-3 code.</exception>
    Iso4217Currency this[string alpha3] { get; }

    /// <summary>Finds a currency by alpha-3 code (case-insensitive), or <c>null</c> if unknown.</summary>
    Iso4217Currency? Find(string alpha3);

    /// <summary>Tries to get a currency by alpha-3 code (case-insensitive).</summary>
    bool TryGet(string alpha3, [NotNullWhen(true)] out Iso4217Currency? currency);

    /// <summary>Finds a currency by ISO 4217 numeric code, or <c>null</c> if unknown.</summary>
    Iso4217Currency? FindByNumericCode(int numericCode);
}