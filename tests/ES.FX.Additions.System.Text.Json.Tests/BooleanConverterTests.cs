using System.Text.Json;
using System.Text.Json.Serialization;
using ES.FX.Additions.System.Text.Json.Serialization.Converters;

namespace ES.FX.Additions.System.Text.Json.Tests;

public class BooleanConverterTests
{
    private static JsonSerializerOptions Options()
    {
        var o = new JsonSerializerOptions();
        o.Converters.Add(new BooleanConverter());
        return o;
    }

    // ---- Factory CanConvert / CreateConverter ----

    [Fact]
    public void CanConvert_BoolAndNullableBool_True()
    {
        var factory = new BooleanConverter();
        Assert.True(factory.CanConvert(typeof(bool)));
        Assert.True(factory.CanConvert(typeof(bool?)));
    }

    [Fact]
    public void CanConvert_OtherTypes_False()
    {
        var factory = new BooleanConverter();
        Assert.False(factory.CanConvert(typeof(int)));
        Assert.False(factory.CanConvert(typeof(string)));
        Assert.False(factory.CanConvert(typeof(DateTimeOffset)));
    }

    [Fact]
    public void CreateConverter_ReturnsDistinctConverters_ForBoolAndNullableBool()
    {
        var factory = new BooleanConverter();
        var options = new JsonSerializerOptions();
        var boolConv = factory.CreateConverter(typeof(bool), options);
        var nullableConv = factory.CreateConverter(typeof(bool?), options);

        Assert.IsAssignableFrom<JsonConverter<bool>>(boolConv);
        Assert.IsAssignableFrom<JsonConverter<bool?>>(nullableConv);
    }

    // ---- Non-nullable bool lenient parse ----

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"false\"", false)]
    [InlineData("\"True\"", true)] // case-insensitive bool.TryParse
    [InlineData("\"FALSE\"", false)]
    [InlineData("\"1\"", true)]
    [InlineData("\"0\"", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void Bool_LenientParse(string valueJson, bool expected)
    {
        var box = JsonSerializer.Deserialize<BoolBox>($"{{\"value\":{valueJson}}}", Options());
        Assert.NotNull(box);
        Assert.Equal(expected, box!.Value);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void Bool_NullOrEmpty_Throws(string valueJson)
    {
        // Non-nullable converter treats null / empty / whitespace as invalid.
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<BoolBox>($"{{\"value\":{valueJson}}}", Options()));
    }

    [Theory]
    [InlineData("\"yes\"")]
    [InlineData("\"maybe\"")]
    [InlineData("2")]
    [InlineData("5")]
    public void Bool_InvalidValues_Throw(string valueJson)
    {
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<BoolBox>($"{{\"value\":{valueJson}}}", Options()));
    }

    // ---- Nullable bool lenient parse ----

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"false\"", false)]
    [InlineData("\"1\"", true)]
    [InlineData("\"0\"", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void NullableBool_LenientParse(string valueJson, bool expected)
    {
        var box = JsonSerializer.Deserialize<NullableBoolBox>($"{{\"value\":{valueJson}}}", Options());
        Assert.NotNull(box);
        Assert.Equal(expected, box!.Value);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public void NullableBool_NullOrEmpty_ReturnsNull(string valueJson)
    {
        var box = JsonSerializer.Deserialize<NullableBoolBox>($"{{\"value\":{valueJson}}}", Options());
        Assert.NotNull(box);
        Assert.Null(box!.Value);
    }

    [Theory]
    [InlineData("\"nope\"")]
    [InlineData("7")]
    public void NullableBool_InvalidValues_Throw(string valueJson)
    {
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<NullableBoolBox>($"{{\"value\":{valueJson}}}", Options()));
    }

    // ---- Write side ----

    [Fact]
    public void Bool_Write_ProducesJsonBoolean()
    {
        var json = JsonSerializer.Serialize(new BoolBox { Value = true }, Options());
        Assert.Contains("\"value\":true", json);
    }

    [Fact]
    public void NullableBool_Write_Null_ProducesJsonNull()
    {
        var options = Options();
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        var json = JsonSerializer.Serialize(new NullableBoolBox { Value = null }, options);
        Assert.Contains("\"value\":null", json);
    }

    [Fact]
    public void NullableBool_Write_Value_ProducesJsonBoolean()
    {
        var json = JsonSerializer.Serialize(new NullableBoolBox { Value = false }, Options());
        Assert.Contains("\"value\":false", json);
    }

    [Fact]
    public void Bool_RoundTrip_FromLenientString()
    {
        // "1" (string) -> true -> serialize -> true
        var box = JsonSerializer.Deserialize<BoolBox>("{\"value\":\"1\"}", Options());
        var json = JsonSerializer.Serialize(box, Options());
        Assert.Contains("\"value\":true", json);
    }

    private sealed class BoolBox
    {
        [JsonPropertyName("value")] public bool Value { get; set; }
    }

    private sealed class NullableBoolBox
    {
        [JsonPropertyName("value")] public bool? Value { get; set; }
    }
}