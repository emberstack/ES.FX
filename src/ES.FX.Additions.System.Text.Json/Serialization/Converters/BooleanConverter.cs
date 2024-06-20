using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace ES.FX.Additions.System.Text.Json.Serialization.Converters;

/// <summary>
///     Converts various JSON representations into a nullable boolean (<see cref="bool" />).
/// </summary>
/// <remarks>
///     This converter supports JSON boolean values, string representations (e.g. "true", "false", "1", "0"),
///     and numeric representations (1 or 0). For JSON null tokens or empty/whitespace strings, it returns <c>null</c>.
/// </remarks>
[PublicAPI]
public class BooleanConverter : JsonConverter<bool?>
{
    /// <summary>
    ///     Reads and converts the JSON representation into a nullable boolean.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <param name="typeToConvert">
    ///     The type of the object to convert. This should be <see cref="bool" /> or
    ///     <see cref="bool" />.
    /// </param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>
    ///     A nullable boolean corresponding to the JSON value, or <c>null</c> if the token is null or an empty string.
    /// </returns>
    /// <exception cref="JsonException">
    ///     Thrown when the JSON token is not a valid representation of a boolean.
    /// </exception>
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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