using JetBrains.Annotations;

namespace ES.FX.Primitives.Extensions;

[PublicAPI]
public static class EnumExtensions
{
    /// <summary>
    ///     Parses the string representation of the name or numeric value of one or more enumerated constants
    ///     to its equivalent enum value.
    /// </summary>
    /// <typeparam name="T">The enum type to convert to.</typeparam>
    /// <param name="value">A string containing the name or numeric value of the enum member.</param>
    /// <param name="ignoreCase">Indicates whether to ignore case during the conversion.</param>
    /// <returns>
    ///     The enum value equivalent to <paramref name="value" />.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="value" /> is not a valid representation of an enum member.
    /// </exception>
    public static T ParseEnum<T>(this string value, bool ignoreCase = true) where T : Enum =>
        (T)Enum.Parse(typeof(T), value, ignoreCase);

    /// <summary>
    ///     Parses the string representation of the name or numeric value of one or more enumerated constants
    ///     to its equivalent enum value. Returns a default value if the conversion fails.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to convert to.</typeparam>
    /// <param name="value">A string containing the name or numeric value of the enum member.</param>
    /// <param name="defaultValue">The default value to return if the conversion fails.</param>
    /// <returns>
    ///     The enum value equivalent to <paramref name="value" /> if the conversion succeeds; otherwise,
    ///     <paramref name="defaultValue" />.
    /// </returns>
    public static TEnum ParseEnumOrDefault<TEnum>(this string value, TEnum defaultValue) where TEnum : struct, Enum
    {
        if (int.TryParse(value, out var intValue))
        {
            if (Enum.IsDefined(typeof(TEnum), intValue)) return (TEnum)Enum.ToObject(typeof(TEnum), intValue);
        }
        else if (Enum.TryParse(value, true, out TEnum result))
        {
            return result;
        }

        return defaultValue;
    }

    /// <summary>
    ///     Converts an enum value to its underlying numeric representation as a string.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="enumValue">The enum value to convert.</param>
    /// <returns>A string representation of the enum's underlying numeric value.</returns>
    public static string ToNumericString<T>(this T enumValue) where T : Enum =>
        Convert.ToInt32(enumValue).ToString();
}