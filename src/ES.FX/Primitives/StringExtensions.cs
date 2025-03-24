using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using JetBrains.Annotations;

namespace ES.FX.Primitives;

[PublicAPI]
public static class StringExtensions
{
    /// <summary>
    ///     Truncates the specified string to the given maximum length.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="length">The maximum allowed length of the string.</param>
    /// <returns>
    ///     The original string if its length is less than or equal to <paramref name="length" />;
    ///     otherwise, a substring containing the first <paramref name="length" /> characters.
    ///     Returns <c>null</c> if the input string is <c>null</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="length" /> is less than zero.
    /// </exception>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Truncate(this string? value, int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

        if (value is null) return null;

        return value.Length <= length ? value : value[..length];
    }


    /// <summary>
    ///     Truncates the specified string to the given maximum length, or returns a default value if the input is null or
    ///     empty.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="length">The maximum allowed length of the string.</param>
    /// <param name="defaultValue">The value to return if the input string is null or empty.</param>
    /// <returns>
    ///     The truncated string if the input is not null or empty; otherwise, the specified default value.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="length" /> is less than zero.
    /// </exception>
    public static string TruncateOrDefault(this string? value, int length, string defaultValue)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

        return string.IsNullOrEmpty(value) ? defaultValue : value.Truncate(length);
    }


    /// <summary>
    ///     Splits the input string into an array of substrings, each with a maximum length specified by
    ///     <paramref name="length" />.
    /// </summary>
    /// <param name="value">The string to split.</param>
    /// <param name="length">
    ///     The maximum length of each resulting substring. Must be greater than zero.
    /// </param>
    /// <returns>
    ///     An array of substrings, where each substring has at most <paramref name="length" /> characters.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="length" /> is less than or equal to zero.
    /// </exception>
    public static string[] SplitIntoChunks(this string? value, int length)
    {
        if (value is null) return [];

        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than zero.");

        return value
            .Chunk(length)
            .Select(chunk => new string(chunk))
            .ToArray();
    }


    /// <summary>
    ///     Converts the specified text to title case using the provided culture.
    ///     If no culture is provided, the invariant culture is used.
    /// </summary>
    /// <param name="value">The text to convert to title case.</param>
    /// <param name="culture">
    ///     The <see cref="CultureInfo" /> to use for conversion.
    ///     If <c>null</c>, <see cref="CultureInfo.InvariantCulture" /> is used.
    /// </param>
    /// <returns>
    ///     A new string where the first character of each word is capitalized and all other characters are in lower case.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value" /> is <c>null</c>.</exception>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? ToTitleCase(this string? value, CultureInfo? culture = null)
    {
        if (value is null) return null;

        culture ??= CultureInfo.InvariantCulture;
        return culture.TextInfo.ToTitleCase(value.ToLower(culture));
    }
}