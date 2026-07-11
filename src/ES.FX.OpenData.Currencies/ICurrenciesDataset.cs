using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.OpenData.Currencies;

/// <summary>
///     Read access to the ISO 4217 currencies dataset. Lookups follow the family contract: the indexer throws
///     for an unknown code, while <see cref="Find" /> / <see cref="TryGet" /> tolerate misses.
/// </summary>
[PublicAPI]
public interface ICurrenciesDataset
{
    /// <summary>All currencies, in source order.</summary>
    IReadOnlyList<Currency> All { get; }

    /// <summary>Gets a currency by ISO 4217 alpha-3 code (case-insensitive).</summary>
    /// <exception cref="KeyNotFoundException">Thrown when the code is not a known alpha-3 code.</exception>
    Currency this[string alpha3] { get; }

    /// <summary>Finds a currency by alpha-3 code (case-insensitive), or <c>null</c> if unknown.</summary>
    Currency? Find(string alpha3);

    /// <summary>Tries to get a currency by alpha-3 code (case-insensitive).</summary>
    bool TryGet(string alpha3, [NotNullWhen(true)] out Currency? currency);

    /// <summary>Finds a currency by ISO 4217 numeric code, or <c>null</c> if unknown.</summary>
    Currency? FindByNumericCode(int numericCode);
}