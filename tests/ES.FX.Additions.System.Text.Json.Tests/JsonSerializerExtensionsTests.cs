using System.Text;
using ES.FX.Additions.System.Text.Json.Serialization;

namespace ES.FX.Additions.System.Text.Json.Tests;

public class JsonSerializerExtensionsTests
{
    private static Stream StreamOf(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    // ==================== TryJsonDeserialize (string) ====================

    [Fact]
    public void TryJsonDeserialize_String_Valid_ReturnsTrue()
    {
        // WebApi default options => camelCase property names.
        var ok = "{\"name\":\"Ada\",\"age\":36}".TryJsonDeserialize<Person>(out var person);
        Assert.True(ok);
        Assert.NotNull(person);
        Assert.Equal("Ada", person!.Name);
        Assert.Equal(36, person.Age);
    }

    [Fact]
    public void TryJsonDeserialize_String_Null_ReturnsFalse()
    {
        string? input = null;
        var ok = input.TryJsonDeserialize<Person>(out var person);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryJsonDeserialize_String_Invalid_ReturnsFalse()
    {
        var ok = "{ this is not json".TryJsonDeserialize<Person>(out var person);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryJsonDeserialize_String_JsonNullLiteral_ReturnsFalse()
    {
        // Deserializes to null reference => method reports false.
        var ok = "null".TryJsonDeserialize<Person>(out var person);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryJsonDeserialize_String_HonorsCustomOptions()
    {
        // Payload uses PascalCase (General defaults); WebApi (default) would not match "Name".
        var ok = "{\"Name\":\"Bob\",\"Age\":5}".TryJsonDeserialize<Person>(out var person,
            JsonSerializerOptionsExtended.Payload);
        Assert.True(ok);
        Assert.Equal("Bob", person!.Name);
    }

    // ==================== TryJsonDeserialize (stream) ====================

    [Fact]
    public void TryJsonDeserialize_Stream_Valid_ReturnsTrue()
    {
        using var stream = StreamOf("{\"name\":\"Ada\",\"age\":36}");
        var ok = stream.TryJsonDeserialize<Person>(out var person);
        Assert.True(ok);
        Assert.Equal("Ada", person!.Name);
    }

    [Fact]
    public void TryJsonDeserialize_Stream_Null_ReturnsFalse()
    {
        Stream? stream = null;
        var ok = stream.TryJsonDeserialize<Person>(out var person);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryJsonDeserialize_Stream_Invalid_ReturnsFalse()
    {
        using var stream = StreamOf("not json at all");
        var ok = stream.TryJsonDeserialize<Person>(out var person);
        Assert.False(ok);
    }

    // ==================== JsonDeserializeOrDefault (string) ====================

    [Fact]
    public void JsonDeserializeOrDefault_String_Valid_ReturnsValue()
    {
        var person = "{\"name\":\"Ada\",\"age\":36}".JsonDeserializeOrDefault<Person>();
        Assert.NotNull(person);
        Assert.Equal("Ada", person!.Name);
    }

    [Fact]
    public void JsonDeserializeOrDefault_String_Null_ReturnsDefault()
    {
        string? input = null;
        var fallback = new Person { Name = "fallback" };
        var person = input.JsonDeserializeOrDefault(fallback);
        Assert.Same(fallback, person);
    }

    [Fact]
    public void JsonDeserializeOrDefault_String_Invalid_ReturnsDefault()
    {
        var fallback = new Person { Name = "fallback" };
        var person = "{bad".JsonDeserializeOrDefault(fallback);
        Assert.Same(fallback, person);
    }

    [Fact]
    public void JsonDeserializeOrDefault_String_JsonNull_ReturnsDefault()
    {
        var fallback = new Person { Name = "fallback" };
        var person = "null".JsonDeserializeOrDefault(fallback);
        Assert.Same(fallback, person);
    }

    // ==================== JsonDeserializeOrDefault (stream) ====================

    [Fact]
    public void JsonDeserializeOrDefault_Stream_Valid_ReturnsValue()
    {
        using var stream = StreamOf("{\"name\":\"Ada\",\"age\":36}");
        var person = stream.JsonDeserializeOrDefault<Person>();
        Assert.NotNull(person);
        Assert.Equal("Ada", person!.Name);
    }

    [Fact]
    public void JsonDeserializeOrDefault_Stream_Null_ReturnsDefault()
    {
        Stream? stream = null;
        var fallback = new Person { Name = "fallback" };
        var person = stream.JsonDeserializeOrDefault(fallback);
        Assert.Same(fallback, person);
    }

    // ==================== ConvertViaJson ====================

    [Fact]
    public void ConvertViaJson_Object_DeepConverts()
    {
        // Source anonymous object -> Person. camelCase names match WebApi defaults.
        var source = new { name = "Ada", age = 36 };
        var person = source.ConvertViaJson<Person>();
        Assert.NotNull(person);
        Assert.Equal("Ada", person!.Name);
        Assert.Equal(36, person.Age);
    }

    [Fact]
    public void ConvertViaJson_StringSource_TreatedAsRawJson()
    {
        // A string source is treated as raw JSON, not a JSON string literal.
        var json = "{\"name\":\"Ada\",\"age\":36}";
        var person = json.ConvertViaJson<Person>();
        Assert.NotNull(person);
        Assert.Equal("Ada", person!.Name);
    }

    [Fact]
    public void ConvertViaJson_Null_ReturnsDefault()
    {
        object? source = null;
        var fallback = new Person { Name = "fallback" };
        var person = source.ConvertViaJson(fallback);
        Assert.Same(fallback, person);
    }

    [Fact]
    public void ConvertViaJson_InvalidStringJson_ReturnsDefault()
    {
        var fallback = new Person { Name = "fallback" };
        var person = "{not valid".ConvertViaJson(fallback);
        Assert.Same(fallback, person);
    }

    // ==================== TryConvertViaJson ====================

    [Fact]
    public void TryConvertViaJson_Object_ReturnsTrue()
    {
        var source = new { name = "Ada", age = 36 };
        var ok = source.TryConvertViaJson<Person>(out var person);
        Assert.True(ok);
        Assert.Equal("Ada", person!.Name);
    }

    [Fact]
    public void TryConvertViaJson_StringSource_RawJson_ReturnsTrue()
    {
        var ok = "{\"name\":\"Ada\",\"age\":36}".TryConvertViaJson<Person>(out var person);
        Assert.True(ok);
        Assert.Equal("Ada", person!.Name);
    }

    [Fact]
    public void TryConvertViaJson_Null_ReturnsFalse()
    {
        object? source = null;
        var ok = source.TryConvertViaJson<Person>(out var person);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryConvertViaJson_InvalidStringJson_ReturnsFalse()
    {
        var ok = "{broken".TryConvertViaJson<Person>(out var person);
        Assert.False(ok);
        Assert.Null(person);
    }

    private sealed class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }
}