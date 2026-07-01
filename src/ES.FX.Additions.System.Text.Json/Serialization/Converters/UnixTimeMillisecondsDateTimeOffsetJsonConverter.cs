using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace ES.FX.Additions.System.Text.Json.Serialization.Converters;

/// <summary>
///     Converts Unix time values to and from <see cref="DateTimeOffset" /> objects.
/// </summary>
/// <remarks>
///     Unix time is expressed here as the number of milliseconds that have elapsed since 00:00:00 UTC on 1 January 1970
///     (the Unix epoch).
///     This converter supports Unix time values represented either as JSON numbers or as strings.
///     When encountering a JSON null or an empty string, the converter throws a <see cref="JsonException" />
/// </remarks>
[PublicAPI]
public class UnixTimeMillisecondsDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader)
        {
            case { TokenType: JsonTokenType.String }:
            {
                var stringValue = reader.GetString();
                if (string.IsNullOrEmpty(stringValue))
                    throw new JsonException("Invalid token type for Unix epoch time");
                if (!long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out var unixTime))
                    throw new JsonException($"Invalid Unix epoch time string: {stringValue}");
                return FromUnixTime(unixTime);
            }
            case { TokenType: JsonTokenType.Number }:
            {
                if (!reader.TryGetInt64(out var unixTime))
                    throw new JsonException("Invalid Unix epoch time number");
                return FromUnixTime(unixTime);
            }
            default:
                throw new JsonException("Invalid token type for Unix epoch time");
        }
    }

    private static DateTimeOffset FromUnixTime(long unixTime)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixTime);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new JsonException($"Invalid Unix epoch time: {unixTime}", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnixTimeMilliseconds());
    }
}