using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace ES.FX.Additions.System.Text.Json.Serialization.Converters;

/// <summary>
///     Converts various JSON representations into <see cref="bool" /> and <see cref="bool" />? values.
/// </summary>
/// <remarks>
///     This factory produces converters for both non-nullable <see cref="bool" /> and nullable
///     <see cref="bool" />? members. Both support JSON boolean values, string representations
///     (e.g. "true", "false", "1", "0"), and numeric representations (1 or 0). For JSON null tokens or
///     empty/whitespace strings, the nullable converter returns <c>null</c>; the non-nullable converter
///     throws a <see cref="JsonException" />.
/// </remarks>
[PublicAPI]
public class BooleanConverter : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(bool) || typeToConvert == typeof(bool?);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        typeToConvert == typeof(bool?)
            ? new NullableBooleanConverter()
            : new NonNullableBooleanConverter();

    /// <summary>
    ///     Reads a lenient boolean representation from the supplied <paramref name="reader" />.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <returns>
    ///     A nullable boolean corresponding to the JSON value, or <c>null</c> if the token is null or an empty string.
    /// </returns>
    /// <exception cref="JsonException">
    ///     Thrown when the JSON token is not a valid representation of a boolean.
    /// </exception>
    private static bool? Read(ref Utf8JsonReader reader)
    {
        switch (reader)
        {
            case { TokenType: JsonTokenType.Null }:
                return null;
            case { TokenType: JsonTokenType.True }:
                return true;
            case { TokenType: JsonTokenType.False }:
                return false;
            case { TokenType: JsonTokenType.String }:
            {
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str)) return null;
                // Try parsing with case-insensitive comparison.
                if (bool.TryParse(str, out var result)) return result;
                // Support numeric string representations.
                if (str == "1") return true;
                if (str == "0") return false;
                throw new JsonException($"Invalid boolean string: {str}");
            }
            case { TokenType: JsonTokenType.Number }:
            {
                // Support numeric representations (1 or 0).
                if (!reader.TryGetInt64(out var num)) throw new JsonException("Invalid numeric boolean value.");
                return num switch
                {
                    1 => true,
                    0 => false,
                    _ => throw new JsonException("Invalid numeric boolean value.")
                };
            }
            default:
                throw new JsonException($"Unexpected token when parsing boolean. Token: {reader.TokenType}");
        }
    }

    /// <summary>
    ///     Converts various JSON representations into a nullable boolean (<see cref="bool" />?).
    /// </summary>
    private sealed class NullableBooleanConverter : JsonConverter<bool?>
    {
        /// <summary>
        ///     Reads and converts the JSON representation into a nullable boolean.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="typeToConvert">
        ///     The type of the object to convert. This should be <see cref="bool" />?.
        /// </param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>
        ///     A nullable boolean corresponding to the JSON value, or <c>null</c> if the token is null or an empty
        ///     string.
        /// </returns>
        /// <exception cref="JsonException">
        ///     Thrown when the JSON token is not a valid representation of a boolean.
        /// </exception>
        public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            BooleanConverter.Read(ref reader);

        /// <summary>
        ///     Writes a nullable boolean as a JSON boolean value. Writes a JSON null if the value is <c>null</c>.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The nullable boolean value to convert into JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteBooleanValue(value.Value);
            else
                writer.WriteNullValue();
        }
    }

    /// <summary>
    ///     Converts various JSON representations into a non-nullable boolean (<see cref="bool" />).
    /// </summary>
    private sealed class NonNullableBooleanConverter : JsonConverter<bool>
    {
        /// <summary>
        ///     Reads and converts the JSON representation into a boolean.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="typeToConvert">
        ///     The type of the object to convert. This should be <see cref="bool" />.
        /// </param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>A boolean corresponding to the JSON value.</returns>
        /// <exception cref="JsonException">
        ///     Thrown when the JSON token is not a valid representation of a boolean, including JSON null tokens and
        ///     empty/whitespace strings.
        /// </exception>
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            BooleanConverter.Read(ref reader) ??
            throw new JsonException("A null or empty boolean value is not valid for a non-nullable boolean.");

        /// <summary>
        ///     Writes a boolean as a JSON boolean value.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The boolean value to convert into JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
            writer.WriteBooleanValue(value);
    }
}