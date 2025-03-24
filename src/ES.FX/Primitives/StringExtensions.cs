using System.Diagnostics.CodeAnalysis;
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
}