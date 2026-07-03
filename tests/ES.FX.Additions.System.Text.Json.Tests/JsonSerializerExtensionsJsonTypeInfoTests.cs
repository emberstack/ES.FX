using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ES.FX.Additions.System.Text.Json.Serialization;

namespace ES.FX.Additions.System.Text.Json.Tests;

/// <summary>
///     Exercises the source-generated / AOT-safe <see cref="JsonTypeInfo{T}" /> overloads of
///     <see cref="JsonSerializerExtensions" />: success paths, null-input short-circuit,
///     exception-swallowing failure paths, and the <c>ArgumentNullException.ThrowIfNull</c> guards.
/// </summary>
public class JsonSerializerExtensionsJsonTypeInfoTests
{
    private static readonly JsonTypeInfo<SgPerson> PersonInfo = SgJsonContext.Default.SgPerson;
    private static readonly JsonTypeInfo<string> StringInfo = SgJsonContext.Default.String;
    private static readonly JsonTypeInfo<int> IntInfo = SgJsonContext.Default.Int32;

    private static Stream StreamOf(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    // ==================== TryJsonDeserialize<T>(string, JsonTypeInfo) ====================

    [Fact]
    public void TryJsonDeserialize_String_TypeInfo_Valid_ReturnsTrue()
    {
        var ok = "{\"Name\":\"Ada\",\"Age\":36}".TryJsonDeserialize(out var person, PersonInfo);
        Assert.True(ok);
        Assert.NotNull(person);
        Assert.Equal("Ada", person!.Name);
        Assert.Equal(36, person.Age);
    }

    [Fact]
    public void TryJsonDeserialize_String_TypeInfo_Null_ReturnsFalse()
    {
        string? input = null;
        var ok = input.TryJsonDeserialize(out var person, PersonInfo);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryJsonDeserialize_String_TypeInfo_Invalid_ReturnsFalse()
    {
        var ok = "{ not json".TryJsonDeserialize(out var person, PersonInfo);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryJsonDeserialize_String_TypeInfo_JsonNullLiteral_ReturnsFalse()
    {
        var ok = "null".TryJsonDeserialize(out var person, PersonInfo);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryJsonDeserialize_String_TypeInfo_NullTypeInfo_Throws()
    {
        // Guard runs only after the null short-circuit, so a non-null payload is required to reach it.
        Assert.Throws<ArgumentNullException>(() =>
            "{}".TryJsonDeserialize(out SgPerson? _, (JsonTypeInfo<SgPerson>)null!));
    }

    // ==================== TryJsonDeserialize<T>(Stream, JsonTypeInfo) ====================

    [Fact]
    public void TryJsonDeserialize_Stream_TypeInfo_Valid_ReturnsTrue()
    {
        using var stream = StreamOf("{\"Name\":\"Ada\",\"Age\":36}");
        var ok = stream.TryJsonDeserialize(out var person, PersonInfo);
        Assert.True(ok);
        Assert.Equal("Ada", person!.Name);
    }

    [Fact]
    public void TryJsonDeserialize_Stream_TypeInfo_Null_ReturnsFalse()
    {
        Stream? stream = null;
        var ok = stream.TryJsonDeserialize(out var person, PersonInfo);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryJsonDeserialize_Stream_TypeInfo_Invalid_ReturnsFalse()
    {
        using var stream = StreamOf("total garbage");
        var ok = stream.TryJsonDeserialize(out var person, PersonInfo);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryJsonDeserialize_Stream_TypeInfo_NullTypeInfo_Throws()
    {
        using var stream = StreamOf("{}");
        Assert.Throws<ArgumentNullException>(() =>
            stream.TryJsonDeserialize(out SgPerson? _, (JsonTypeInfo<SgPerson>)null!));
    }

    // ==================== JsonDeserializeOrDefault<T>(string, JsonTypeInfo) ====================

    [Fact]
    public void JsonDeserializeOrDefault_String_TypeInfo_Valid_ReturnsValue()
    {
        var person = "{\"Name\":\"Ada\",\"Age\":36}".JsonDeserializeOrDefault(PersonInfo);
        Assert.NotNull(person);
        Assert.Equal("Ada", person!.Name);
    }

    [Fact]
    public void JsonDeserializeOrDefault_String_TypeInfo_Null_ReturnsDefault()
    {
        string? input = null;
        var fallback = new SgPerson { Name = "fallback" };
        var person = input.JsonDeserializeOrDefault(PersonInfo, fallback);
        Assert.Same(fallback, person);
    }

    [Fact]
    public void JsonDeserializeOrDefault_String_TypeInfo_Invalid_ReturnsDefault()
    {
        var fallback = new SgPerson { Name = "fallback" };
        var person = "{bad".JsonDeserializeOrDefault(PersonInfo, fallback);
        Assert.Same(fallback, person);
    }

    [Fact]
    public void JsonDeserializeOrDefault_String_TypeInfo_JsonNull_ReturnsDefault()
    {
        var fallback = new SgPerson { Name = "fallback" };
        var person = "null".JsonDeserializeOrDefault(PersonInfo, fallback);
        Assert.Same(fallback, person);
    }

    [Fact]
    public void JsonDeserializeOrDefault_String_TypeInfo_NullTypeInfo_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            "{}".JsonDeserializeOrDefault((JsonTypeInfo<SgPerson>)null!));
    }

    // ==================== JsonDeserializeOrDefault<T>(Stream, JsonTypeInfo) ====================

    [Fact]
    public void JsonDeserializeOrDefault_Stream_TypeInfo_Valid_ReturnsValue()
    {
        using var stream = StreamOf("{\"Name\":\"Ada\",\"Age\":36}");
        var person = stream.JsonDeserializeOrDefault(PersonInfo);
        Assert.NotNull(person);
        Assert.Equal("Ada", person!.Name);
    }

    [Fact]
    public void JsonDeserializeOrDefault_Stream_TypeInfo_Null_ReturnsDefault()
    {
        Stream? stream = null;
        var fallback = new SgPerson { Name = "fallback" };
        var person = stream.JsonDeserializeOrDefault(PersonInfo, fallback);
        Assert.Same(fallback, person);
    }

    [Fact]
    public void JsonDeserializeOrDefault_Stream_TypeInfo_Invalid_ReturnsDefault()
    {
        using var stream = StreamOf("nope");
        var fallback = new SgPerson { Name = "fallback" };
        var person = stream.JsonDeserializeOrDefault(PersonInfo, fallback);
        Assert.Same(fallback, person);
    }

    [Fact]
    public void JsonDeserializeOrDefault_Stream_TypeInfo_NullTypeInfo_Throws()
    {
        using var stream = StreamOf("{}");
        Assert.Throws<ArgumentNullException>(() =>
            stream.JsonDeserializeOrDefault((JsonTypeInfo<SgPerson>)null!));
    }

    // ==================== ConvertViaJson<TSource, T> ====================

    [Fact]
    public void ConvertViaJson_TypeInfo_DeepConverts()
    {
        var source = new SgPerson { Name = "Ada", Age = 36 };
        var clone = source.ConvertViaJson(PersonInfo, PersonInfo);
        Assert.NotNull(clone);
        Assert.NotSame(source, clone);
        Assert.Equal("Ada", clone!.Name);
        Assert.Equal(36, clone.Age);
    }

    [Fact]
    public void ConvertViaJson_TypeInfo_Null_ReturnsDefault()
    {
        SgPerson? source = null;
        var fallback = new SgPerson { Name = "fallback" };
        var result = source.ConvertViaJson(PersonInfo, PersonInfo, fallback);
        Assert.Same(fallback, result);
    }

    [Fact]
    public void ConvertViaJson_TypeInfo_StringSource_SerializedAsJsonLiteral_NotRawJson()
    {
        // Documented divergence from the object overload: a string source is serialized as a JSON
        // string literal via sourceJsonTypeInfo, NOT treated as raw JSON. So a raw JSON object string
        // does NOT deserialize into SgPerson — it round-trips back to the same string.
        var jsonObject = "{\"Name\":\"Ada\",\"Age\":36}";

        // Target = string: the literal round-trips to the identical string.
        var asString = jsonObject.ConvertViaJson(StringInfo, StringInfo);
        Assert.Equal(jsonObject, asString);

        // Target = SgPerson: attempting to read a JSON *string* as an object fails -> default (null).
        var asPerson = jsonObject.ConvertViaJson(StringInfo, PersonInfo);
        Assert.Null(asPerson);
    }

    [Fact]
    public void ConvertViaJson_TypeInfo_NullSourceTypeInfo_Throws()
    {
        var source = new SgPerson { Name = "Ada" };
        Assert.Throws<ArgumentNullException>(() =>
            source.ConvertViaJson((JsonTypeInfo<SgPerson>)null!, PersonInfo));
    }

    [Fact]
    public void ConvertViaJson_TypeInfo_NullTargetTypeInfo_Throws()
    {
        var source = new SgPerson { Name = "Ada" };
        Assert.Throws<ArgumentNullException>(() =>
            source.ConvertViaJson(PersonInfo, (JsonTypeInfo<SgPerson>)null!));
    }

    // ==================== TryConvertViaJson<TSource, T> ====================

    [Fact]
    public void TryConvertViaJson_TypeInfo_ReturnsTrue()
    {
        var source = new SgPerson { Name = "Ada", Age = 36 };
        var ok = source.TryConvertViaJson(out var clone, PersonInfo, PersonInfo);
        Assert.True(ok);
        Assert.NotNull(clone);
        Assert.Equal("Ada", clone!.Name);
        Assert.Equal(36, clone.Age);
    }

    [Fact]
    public void TryConvertViaJson_TypeInfo_Null_ReturnsFalse()
    {
        SgPerson? source = null;
        var ok = source.TryConvertViaJson(out var clone, PersonInfo, PersonInfo);
        Assert.False(ok);
        Assert.Null(clone);
    }

    [Fact]
    public void TryConvertViaJson_TypeInfo_IncompatibleTarget_ReturnsFalse()
    {
        // Serialize an int (JSON number) then try to read it as SgPerson -> fails -> false.
        var ok = 42.TryConvertViaJson(out SgPerson? person, IntInfo, PersonInfo);
        Assert.False(ok);
        Assert.Null(person);
    }

    [Fact]
    public void TryConvertViaJson_TypeInfo_NullSourceTypeInfo_Throws()
    {
        var source = new SgPerson { Name = "Ada" };
        Assert.Throws<ArgumentNullException>(() =>
            source.TryConvertViaJson(out SgPerson? _, (JsonTypeInfo<SgPerson>)null!, PersonInfo));
    }

    [Fact]
    public void TryConvertViaJson_TypeInfo_NullTargetTypeInfo_Throws()
    {
        var source = new SgPerson { Name = "Ada" };
        Assert.Throws<ArgumentNullException>(() =>
            source.TryConvertViaJson(out SgPerson? _, PersonInfo, (JsonTypeInfo<SgPerson>)null!));
    }
}

/// <summary>Simple DTO for source-generated serialization tests.</summary>
public sealed class SgPerson
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

/// <summary>Source-generated JSON context providing AOT-safe <see cref="JsonTypeInfo{T}" /> metadata.</summary>
[JsonSerializable(typeof(SgPerson))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
internal partial class SgJsonContext : JsonSerializerContext;
