using ES.FX.Primitives.Extensions;

namespace ES.FX.OpenData.Romania.TerritorialUnits.Internal;

/// <summary>Text helpers for building and querying searchable, diacritic-insensitive SIRUTA names.</summary>
internal static class OpenDataText
{
    /// <summary>
    ///     Folds a name into its canonical searchable form: diacritics removed (handles both cedilla and
    ///     comma-below encodings identically), hyphens replaced with spaces, trimmed, and lower-cased with the
    ///     invariant culture. Apply the same fold to both stored names and incoming search queries.
    /// </summary>
    public static string Fold(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FoldStripped(value.RemoveDiacritics());
    }

    /// <summary>
    ///     Normalizes a name into a diacritic-free display form: diacritics removed, trimmed, and title-cased
    ///     (e.g. <c>"BRĂILA"</c> → <c>"Braila"</c>). Unlike <see cref="Fold" /> it keeps hyphens and casing,
    ///     so it stays readable for ASCII-only display or interop.
    /// </summary>
    public static string NormalizeForDisplay(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return NormalizeForDisplayStripped(value.RemoveDiacritics());
    }

    /// <summary>
    ///     <see cref="Fold" /> applied to a string whose diacritics have ALREADY been removed. Lets a caller that
    ///     needs both normalized forms remove diacritics once — the expensive step — and derive both from the result.
    /// </summary>
    public static string FoldStripped(string diacriticFree) =>
        diacriticFree.Replace('-', ' ').Trim().ToLowerInvariant();

    /// <summary><see cref="NormalizeForDisplay" /> applied to an already diacritic-free string.</summary>
    public static string NormalizeForDisplayStripped(string diacriticFree) =>
        diacriticFree.Trim().ToTitleCase();
}