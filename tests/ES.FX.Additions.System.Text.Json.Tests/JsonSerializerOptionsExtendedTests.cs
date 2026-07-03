using System.Text.Json;
using ES.FX.Additions.System.Text.Json.Serialization;

namespace ES.FX.Additions.System.Text.Json.Tests;

public class JsonSerializerOptionsExtendedTests
{
    public enum Color
    {
        Red = 0,
        DarkGreen = 1,
        Blue = 2
    }

    private sealed class Sample
    {
        public string FirstName { get; set; } = "";
        public Color Favorite { get; set; }
    }

    // ---- IsReadOnly ----

    [Fact]
    public void WebApi_IsReadOnly()
    {
        Assert.True(JsonSerializerOptionsExtended.WebApi.IsReadOnly);
    }

    [Fact]
    public void JavascriptWebApi_IsReadOnly()
    {
        Assert.True(JsonSerializerOptionsExtended.JavascriptWebApi.IsReadOnly);
    }

    [Fact]
    public void Payload_IsReadOnly()
    {
        Assert.True(JsonSerializerOptionsExtended.Payload.IsReadOnly);
    }

    [Fact]
    public void ReadOnly_Instance_ThrowsWhenMutated()
    {
        // A read-only options instance must reject mutation (guards process-wide state).
        Assert.Throws<InvalidOperationException>(() =>
            JsonSerializerOptionsExtended.WebApi.PropertyNameCaseInsensitive = false);
    }

    [Fact]
    public void ReadOnly_Instances_AreStableSingletons()
    {
        // Property getters must return the same cached instance each call.
        Assert.Same(JsonSerializerOptionsExtended.WebApi, JsonSerializerOptionsExtended.WebApi);
        Assert.Same(JsonSerializerOptionsExtended.Payload, JsonSerializerOptionsExtended.Payload);
        Assert.Same(JsonSerializerOptionsExtended.JavascriptWebApi, JsonSerializerOptionsExtended.JavascriptWebApi);
    }

    // ---- Casing: Web defaults use camelCase property names ----

    [Fact]
    public void WebApi_SerializesPropertyNames_AsCamelCase()
    {
        var json = JsonSerializer.Serialize(new Sample { FirstName = "Ada" },
            JsonSerializerOptionsExtended.WebApi);
        Assert.Contains("\"firstName\"", json);
        Assert.DoesNotContain("\"FirstName\"", json);
    }

    [Fact]
    public void JavascriptWebApi_SerializesPropertyNames_AsCamelCase()
    {
        var json = JsonSerializer.Serialize(new Sample { FirstName = "Ada" },
            JsonSerializerOptionsExtended.JavascriptWebApi);
        Assert.Contains("\"firstName\"", json);
    }

    [Fact]
    public void Payload_UsesGeneralDefaults_PascalCasePropertyNames()
    {
        // JsonSerializerDefaults.General does not apply a camelCase property naming policy.
        var json = JsonSerializer.Serialize(new Sample { FirstName = "Ada" },
            JsonSerializerOptionsExtended.Payload);
        Assert.Contains("\"FirstName\"", json);
    }

    [Fact]
    public void Payload_DeserializesPropertyNames_CaseInsensitive()
    {
        // PropertyNameCaseInsensitive = true on Payload.
        var sample = JsonSerializer.Deserialize<Sample>("{\"firstname\":\"Ada\"}",
            JsonSerializerOptionsExtended.Payload);
        Assert.NotNull(sample);
        Assert.Equal("Ada", sample!.FirstName);
    }

    [Fact]
    public void WebApi_DeserializesPropertyNames_CaseInsensitive()
    {
        // Web defaults are case-insensitive for property names.
        var sample = JsonSerializer.Deserialize<Sample>("{\"FIRSTNAME\":\"Ada\"}",
            JsonSerializerOptionsExtended.WebApi);
        Assert.NotNull(sample);
        Assert.Equal("Ada", sample!.FirstName);
    }

    // ---- Enum-as-string serialization ----

    [Fact]
    public void WebApi_SerializesEnum_AsString()
    {
        var json = JsonSerializer.Serialize(new Sample { Favorite = Color.DarkGreen },
            JsonSerializerOptionsExtended.WebApi);
        // Default naming policy (null) => enum member name verbatim.
        Assert.Contains("\"DarkGreen\"", json);
        Assert.DoesNotContain(":1", json);
    }

    [Fact]
    public void Payload_SerializesEnum_AsString()
    {
        var json = JsonSerializer.Serialize(new Sample { Favorite = Color.DarkGreen },
            JsonSerializerOptionsExtended.Payload);
        Assert.Contains("\"DarkGreen\"", json);
    }

    // ---- Enum round-trips (string form) ----

    [Theory]
    [InlineData(Color.Red)]
    [InlineData(Color.DarkGreen)]
    [InlineData(Color.Blue)]
    public void WebApi_EnumRoundTrip(Color value)
    {
        var json = JsonSerializer.Serialize(new Sample { Favorite = value },
            JsonSerializerOptionsExtended.WebApi);
        var back = JsonSerializer.Deserialize<Sample>(json, JsonSerializerOptionsExtended.WebApi);
        Assert.NotNull(back);
        Assert.Equal(value, back!.Favorite);
    }

    [Fact]
    public void WebApi_EnumString_MatchedCaseInsensitively()
    {
        // JsonStringEnumConverter matches enum names case-insensitively on read.
        var sample = JsonSerializer.Deserialize<Sample>("{\"favorite\":\"darkgreen\"}",
            JsonSerializerOptionsExtended.WebApi);
        Assert.NotNull(sample);
        Assert.Equal(Color.DarkGreen, sample!.Favorite);
    }

    // ---- Integer enum handling difference ----

    [Fact]
    public void Payload_AllowsIntegerEnumValues_OnRead()
    {
        // Payload's JsonStringEnumConverter is created with default allowIntegerValues=true.
        var sample = JsonSerializer.Deserialize<Sample>("{\"Favorite\":1}",
            JsonSerializerOptionsExtended.Payload);
        Assert.NotNull(sample);
        Assert.Equal(Color.DarkGreen, sample!.Favorite);
    }

    [Fact]
    public void WebApi_RejectsIntegerEnumValues_OnRead()
    {
        // WebApi's converter is created with allowIntegerValues=false.
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<Sample>("{\"favorite\":1}",
                JsonSerializerOptionsExtended.WebApi));
    }

    [Fact]
    public void JavascriptWebApi_RejectsIntegerEnumValues_OnRead()
    {
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<Sample>("{\"favorite\":1}",
                JsonSerializerOptionsExtended.JavascriptWebApi));
    }

    [Fact]
    public void FullRoundTrip_WebApi_PreservesData()
    {
        var original = new Sample { FirstName = "Grace", Favorite = Color.Blue };
        var json = JsonSerializer.Serialize(original, JsonSerializerOptionsExtended.WebApi);
        var back = JsonSerializer.Deserialize<Sample>(json, JsonSerializerOptionsExtended.WebApi);
        Assert.NotNull(back);
        Assert.Equal(original.FirstName, back!.FirstName);
        Assert.Equal(original.Favorite, back.Favorite);
    }
}
