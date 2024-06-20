using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace ES.FX.Additions.System.Text.Json.Serialization.Converters;

/// <summary>
///     Converts Unix time values to and from nullable <see cref="DateTimeOffset" /> objects.
/// </summary>
/// <remarks>
///     Unix time is defined as the number of seconds that have elapsed since 00:00:00 UTC on 1 January 1970 (the Unix
///     epoch).
///     This converter supports Unix time values represented either as JSON numbers or as strings.
///     When encountering a JSON null or an empty string, the converter returns <c>null</c>.
/// </remarks>
[PublicAPI]
public class UnixTimeMillisecondsNullableDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader)
        {
            case { TokenType: JsonTokenType.Null }:
                return null;
            case { TokenType: JsonTokenType.String }:
            {
                var stringValue = reader.GetString();
                // Return null if the string is empty
                if (string.IsNullOrEmpty(stringValue)) return null;
                if (!long.TryParse(stringValue, out var unixTime))
                    throw new JsonException($"Invalid Unix epoch time string: {stringValue}");
                return DateTimeOffset.FromUnixTimeMilliseconds(unixTime);
            }
            case { TokenType: JsonTokenType.Number }:
            {
                var unixTime = reader.GetInt64();
                return DateTimeOffset.FromUnixTimeMilliseconds(unixTime);
            }
            default:
                throw new JsonException("Invalid token type for Unix epoch time");
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            var unixTime = value.Value.ToUnixTimeMilliseconds();
            writer.WriteNumberValue(unixTime);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}