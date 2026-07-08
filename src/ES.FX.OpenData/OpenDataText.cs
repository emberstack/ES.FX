using ES.FX.Primitives.Extensions;
using JetBrains.Annotations;

namespace ES.FX.OpenData;

/// <summary>Shared text helpers for building and querying searchable, diacritic-insensitive dataset keys.</summary>
[PublicAPI]
public static class OpenDataText
{
    /// <summary>
    ///     Folds a name into its canonical searchable form: diacritics removed (handles both cedilla and
    ///     comma-below encodings identically), hyphens replaced with spaces, trimmed, and lower-cased with the
    ///     invariant culture. Apply the same fold to both stored names and incoming search queries so matching is
    ///     independent of accents, case, and hyphenation.
    /// </summary>
    public static string Fold(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.RemoveDiacritics().Replace('-', ' ').Trim().ToLowerInvariant();
    }
}
