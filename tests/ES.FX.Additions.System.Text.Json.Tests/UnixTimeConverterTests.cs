using System.Text.Json;
using System.Text.Json.Serialization;
using ES.FX.Additions.System.Text.Json.Serialization.Converters;

namespace ES.FX.Additions.System.Text.Json.Tests;

public class UnixTimeConverterTests
{
    // 2021-01-01T00:00:00Z
    private const long Seconds = 1609459200L;
    private const long Milliseconds = 1609459200000L;
    private static readonly DateTimeOffset Expected = DateTimeOffset.FromUnixTimeSeconds(Seconds);

    private static JsonSerializerOptions With(JsonConverter converter)
    {
        var o = new JsonSerializerOptions();
        o.Converters.Add(converter);
        return o;
    }

    // ==================== SECONDS (non-nullable) ====================

    [Fact]
    public void Seconds_ReadNumber()
    {
        var o = With(new UnixTimeSecondsDateTimeOffsetJsonConverter());
        var box = JsonSerializer.Deserialize<DtoBox>($"{{\"t\":{Seconds}}}", o);
        Assert.NotNull(box);
        Assert.Equal(Expected, box!.T);
    }

    [Fact]
    public void Seconds_ReadString()
    {
        var o = With(new UnixTimeSecondsDateTimeOffsetJsonConverter());
        var box = JsonSerializer.Deserialize<DtoBox>($"{{\"t\":\"{Seconds}\"}}", o);
        Assert.NotNull(box);
        Assert.Equal(Expected, box!.T);
    }

    [Fact]
    public void Seconds_Write_ProducesSecondsNumber()
    {
        var o = With(new UnixTimeSecondsDateTimeOffsetJsonConverter());
        var json = JsonSerializer.Serialize(new DtoBox { T = Expected }, o);
        Assert.Contains($"\"t\":{Seconds}", json);
    }

    [Fact]
    public void Seconds_RoundTrip()
    {
        var o = With(new UnixTimeSecondsDateTimeOffsetJsonConverter());
        var json = JsonSerializer.Serialize(new DtoBox { T = Expected }, o);
        var back = JsonSerializer.Deserialize<DtoBox>(json, o);
        Assert.Equal(Expected, back!.T);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"notanumber\"")]
    [InlineData("true")]
    public void Seconds_InvalidOrNull_Throws(string valueJson)
    {
        var o = With(new UnixTimeSecondsDateTimeOffsetJsonConverter());
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<DtoBox>($"{{\"t\":{valueJson}}}", o));
    }

    // ==================== SECONDS (nullable) ====================

    [Fact]
    public void NullableSeconds_ReadNumber()
    {
        var o = With(new UnixTimeSecondsNullableDateTimeOffsetJsonConverter());
        var box = JsonSerializer.Deserialize<NullableDtoBox>($"{{\"t\":{Seconds}}}", o);
        Assert.Equal(Expected, box!.T);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public void NullableSeconds_NullOrEmpty_ReturnsNull(string valueJson)
    {
        var o = With(new UnixTimeSecondsNullableDateTimeOffsetJsonConverter());
        var box = JsonSerializer.Deserialize<NullableDtoBox>($"{{\"t\":{valueJson}}}", o);
        Assert.NotNull(box);
        Assert.Null(box!.T);
    }

    [Theory]
    [InlineData("\"bogus\"")]
    [InlineData("true")]
    public void NullableSeconds_Invalid_Throws(string valueJson)
    {
        var o = With(new UnixTimeSecondsNullableDateTimeOffsetJsonConverter());
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<NullableDtoBox>($"{{\"t\":{valueJson}}}", o));
    }

    [Fact]
    public void NullableSeconds_Write_Null_ProducesJsonNull()
    {
        var o = With(new UnixTimeSecondsNullableDateTimeOffsetJsonConverter());
        o.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        var json = JsonSerializer.Serialize(new NullableDtoBox { T = null }, o);
        Assert.Contains("\"t\":null", json);
    }

    [Fact]
    public void NullableSeconds_Write_Value_ProducesSecondsNumber()
    {
        var o = With(new UnixTimeSecondsNullableDateTimeOffsetJsonConverter());
        var json = JsonSerializer.Serialize(new NullableDtoBox { T = Expected }, o);
        Assert.Contains($"\"t\":{Seconds}", json);
    }

    // ==================== MILLISECONDS (non-nullable) ====================

    [Fact]
    public void Milliseconds_ReadNumber()
    {
        var o = With(new UnixTimeMillisecondsDateTimeOffsetJsonConverter());
        var box = JsonSerializer.Deserialize<DtoBox>($"{{\"t\":{Milliseconds}}}", o);
        Assert.Equal(Expected, box!.T);
    }

    [Fact]
    public void Milliseconds_ReadString()
    {
        var o = With(new UnixTimeMillisecondsDateTimeOffsetJsonConverter());
        var box = JsonSerializer.Deserialize<DtoBox>($"{{\"t\":\"{Milliseconds}\"}}", o);
        Assert.Equal(Expected, box!.T);
    }

    [Fact]
    public void Milliseconds_Write_ProducesMillisecondsNumber()
    {
        var o = With(new UnixTimeMillisecondsDateTimeOffsetJsonConverter());
        var json = JsonSerializer.Serialize(new DtoBox { T = Expected }, o);
        Assert.Contains($"\"t\":{Milliseconds}", json);
    }

    [Fact]
    public void Milliseconds_RoundTrip()
    {
        var o = With(new UnixTimeMillisecondsDateTimeOffsetJsonConverter());
        var json = JsonSerializer.Serialize(new DtoBox { T = Expected }, o);
        var back = JsonSerializer.Deserialize<DtoBox>(json, o);
        Assert.Equal(Expected, back!.T);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"nope\"")]
    public void Milliseconds_InvalidOrNull_Throws(string valueJson)
    {
        var o = With(new UnixTimeMillisecondsDateTimeOffsetJsonConverter());
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<DtoBox>($"{{\"t\":{valueJson}}}", o));
    }

    // ==================== MILLISECONDS (nullable) ====================

    [Fact]
    public void NullableMilliseconds_ReadNumber()
    {
        var o = With(new UnixTimeMillisecondsNullableDateTimeOffsetJsonConverter());
        var box = JsonSerializer.Deserialize<NullableDtoBox>($"{{\"t\":{Milliseconds}}}", o);
        Assert.Equal(Expected, box!.T);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public void NullableMilliseconds_NullOrEmpty_ReturnsNull(string valueJson)
    {
        var o = With(new UnixTimeMillisecondsNullableDateTimeOffsetJsonConverter());
        var box = JsonSerializer.Deserialize<NullableDtoBox>($"{{\"t\":{valueJson}}}", o);
        Assert.NotNull(box);
        Assert.Null(box!.T);
    }

    [Fact]
    public void NullableMilliseconds_Invalid_Throws()
    {
        var o = With(new UnixTimeMillisecondsNullableDateTimeOffsetJsonConverter());
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<NullableDtoBox>("{\"t\":\"bad\"}", o));
    }

    [Fact]
    public void NullableMilliseconds_Write_Value_ProducesMillisecondsNumber()
    {
        var o = With(new UnixTimeMillisecondsNullableDateTimeOffsetJsonConverter());
        var json = JsonSerializer.Serialize(new NullableDtoBox { T = Expected }, o);
        Assert.Contains($"\"t\":{Milliseconds}", json);
    }

    // ==================== distinctness: seconds vs milliseconds ====================

    [Fact]
    public void Seconds_And_Milliseconds_ProduceDifferentMagnitudes()
    {
        var secO = With(new UnixTimeSecondsDateTimeOffsetJsonConverter());
        var msO = With(new UnixTimeMillisecondsDateTimeOffsetJsonConverter());
        var secJson = JsonSerializer.Serialize(new DtoBox { T = Expected }, secO);
        var msJson = JsonSerializer.Serialize(new DtoBox { T = Expected }, msO);
        Assert.Contains($"{Seconds}", secJson);
        Assert.Contains($"{Milliseconds}", msJson);
        Assert.NotEqual(secJson, msJson);
    }

    // ==================== FromUnixTime out-of-range -> JsonException wrapping ====================
    // long.MaxValue exceeds the range accepted by DateTimeOffset.FromUnixTimeSeconds/Milliseconds,
    // so the converter's try/catch must re-wrap the ArgumentOutOfRangeException as a JsonException
    // (with the original exception preserved as InnerException).

    [Fact]
    public void Seconds_OutOfRangeNumber_ThrowsJsonException_WrappingArgumentOutOfRange()
    {
        var o = With(new UnixTimeSecondsDateTimeOffsetJsonConverter());
        var ex = Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<DtoBox>($"{{\"t\":{long.MaxValue}}}", o));
        Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
    }

    [Fact]
    public void Seconds_OutOfRangeString_ThrowsJsonException_WrappingArgumentOutOfRange()
    {
        var o = With(new UnixTimeSecondsDateTimeOffsetJsonConverter());
        var ex = Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<DtoBox>($"{{\"t\":\"{long.MaxValue}\"}}", o));
        Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
    }

    [Fact]
    public void NullableSeconds_OutOfRangeNumber_ThrowsJsonException_WrappingArgumentOutOfRange()
    {
        var o = With(new UnixTimeSecondsNullableDateTimeOffsetJsonConverter());
        var ex = Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<NullableDtoBox>($"{{\"t\":{long.MaxValue}}}", o));
        Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
    }

    [Fact]
    public void Milliseconds_OutOfRangeNumber_ThrowsJsonException_WrappingArgumentOutOfRange()
    {
        var o = With(new UnixTimeMillisecondsDateTimeOffsetJsonConverter());
        var ex = Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<DtoBox>($"{{\"t\":{long.MaxValue}}}", o));
        Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
    }

    [Fact]
    public void Milliseconds_OutOfRangeString_ThrowsJsonException_WrappingArgumentOutOfRange()
    {
        var o = With(new UnixTimeMillisecondsDateTimeOffsetJsonConverter());
        var ex = Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<DtoBox>($"{{\"t\":\"{long.MaxValue}\"}}", o));
        Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
    }

    [Fact]
    public void NullableMilliseconds_OutOfRangeNumber_ThrowsJsonException_WrappingArgumentOutOfRange()
    {
        var o = With(new UnixTimeMillisecondsNullableDateTimeOffsetJsonConverter());
        var ex = Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<NullableDtoBox>($"{{\"t\":{long.MaxValue}}}", o));
        Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
    }

    private sealed class DtoBox
    {
        [JsonPropertyName("t")] public DateTimeOffset T { get; set; }
    }

    private sealed class NullableDtoBox
    {
        [JsonPropertyName("t")] public DateTimeOffset? T { get; set; }
    }
}