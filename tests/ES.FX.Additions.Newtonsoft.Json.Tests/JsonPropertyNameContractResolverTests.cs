using System.Text.Json.Serialization;
using ES.FX.Additions.Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ES.FX.Additions.Newtonsoft.Json.Tests;

public class JsonPropertyNameContractResolverTests
{
    private static JsonSerializerSettings Settings() => new()
    {
        ContractResolver = new JsonPropertyNameContractResolver()
    };

    // ------- POCOs -------

    private sealed class RenamedPoco
    {
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }

        [JsonPropertyName("last_name")] public string? LastName { get; set; }

        // No attribute: should keep the CLR member name.
        public int Age { get; set; }
    }

    private sealed class PrecedencePoco
    {
        // System.Text.Json attribute must win over Newtonsoft's [JsonProperty].
        [JsonProperty("newtonsoft_name")]
        [JsonPropertyName("system_text_json_name")]
        public string? Value { get; set; }
    }

    private sealed class NewtonsoftOnlyPoco
    {
        // Only Newtonsoft's attribute present -> the resolver must not disturb it.
        [JsonProperty("nsj_only")] public string? Value { get; set; }
    }

    private sealed class FieldPoco
    {
        [JsonPropertyName("renamed_field")]
        [JsonProperty] // opt the public field into serialization
        public string? RawField;
    }

    // ------- Serialization -------

    [Fact]
    public void Serialize_HonorsSystemTextJsonPropertyName()
    {
        var poco = new RenamedPoco { FirstName = "Ada", LastName = "Lovelace", Age = 36 };

        var json = JsonConvert.SerializeObject(poco, Settings());
        var obj = JObject.Parse(json);

        Assert.Equal("Ada", (string?)obj["first_name"]);
        Assert.Equal("Lovelace", (string?)obj["last_name"]);
        Assert.Equal(36, (int?)obj["Age"]);

        // The original CLR names must not leak through.
        Assert.Null(obj["FirstName"]);
        Assert.Null(obj["LastName"]);
    }

    [Fact]
    public void Serialize_WithoutResolver_UsesClrNames()
    {
        // Sanity check: without the resolver, [JsonPropertyName] is ignored by Newtonsoft.
        var poco = new RenamedPoco { FirstName = "Ada" };

        var json = JsonConvert.SerializeObject(poco);
        var obj = JObject.Parse(json);

        Assert.Equal("Ada", (string?)obj["FirstName"]);
        Assert.Null(obj["first_name"]);
    }

    // ------- Deserialization -------

    [Fact]
    public void Deserialize_HonorsSystemTextJsonPropertyName()
    {
        const string json = """{"first_name":"Grace","last_name":"Hopper","Age":85}""";

        var poco = JsonConvert.DeserializeObject<RenamedPoco>(json, Settings());

        Assert.NotNull(poco);
        Assert.Equal("Grace", poco!.FirstName);
        Assert.Equal("Hopper", poco.LastName);
        Assert.Equal(85, poco.Age);
    }

    [Fact]
    public void Deserialize_ClrNamedPayload_DoesNotBindRenamedProperties()
    {
        // With the resolver active the property is bound to "first_name",
        // so a payload using the CLR name should not populate it.
        const string json = """{"FirstName":"Grace"}""";

        var poco = JsonConvert.DeserializeObject<RenamedPoco>(json, Settings());

        Assert.NotNull(poco);
        Assert.Null(poco!.FirstName);
    }

    // ------- Round trip -------

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var original = new RenamedPoco { FirstName = "Katherine", LastName = "Johnson", Age = 101 };

        var json = JsonConvert.SerializeObject(original, Settings());
        var restored = JsonConvert.DeserializeObject<RenamedPoco>(json, Settings());

        Assert.NotNull(restored);
        Assert.Equal(original.FirstName, restored!.FirstName);
        Assert.Equal(original.LastName, restored.LastName);
        Assert.Equal(original.Age, restored.Age);
    }

    // ------- Precedence over Newtonsoft [JsonProperty] -------

    [Fact]
    public void Serialize_SystemTextJsonName_TakesPrecedenceOver_NewtonsoftJsonProperty()
    {
        var poco = new PrecedencePoco { Value = "x" };

        var json = JsonConvert.SerializeObject(poco, Settings());
        var obj = JObject.Parse(json);

        Assert.Equal("x", (string?)obj["system_text_json_name"]);
        Assert.Null(obj["newtonsoft_name"]);
    }

    [Fact]
    public void Deserialize_SystemTextJsonName_TakesPrecedenceOver_NewtonsoftJsonProperty()
    {
        const string json = """{"system_text_json_name":"win","newtonsoft_name":"lose"}""";

        var poco = JsonConvert.DeserializeObject<PrecedencePoco>(json, Settings());

        Assert.NotNull(poco);
        Assert.Equal("win", poco!.Value);
    }

    // ------- Falls back to Newtonsoft attribute when no STJ attribute -------

    [Fact]
    public void Serialize_WithoutSystemTextJsonName_KeepsNewtonsoftJsonProperty()
    {
        var poco = new NewtonsoftOnlyPoco { Value = "y" };

        var json = JsonConvert.SerializeObject(poco, Settings());
        var obj = JObject.Parse(json);

        Assert.Equal("y", (string?)obj["nsj_only"]);
        Assert.Null(obj["Value"]);
    }

    // ------- Works on fields, not just properties -------

    [Fact]
    public void Serialize_HonorsSystemTextJsonName_OnFields()
    {
        var poco = new FieldPoco { RawField = "z" };

        var json = JsonConvert.SerializeObject(poco, Settings());
        var obj = JObject.Parse(json);

        Assert.Equal("z", (string?)obj["renamed_field"]);
        Assert.Null(obj["RawField"]);
    }

    // ------- Resolver is a DefaultContractResolver -------

    [Fact]
    public void Resolver_IsDefaultContractResolver()
    {
        Assert.IsAssignableFrom<global::Newtonsoft.Json.Serialization.DefaultContractResolver>(
            new JsonPropertyNameContractResolver());
    }

    // ------- Works when supplied via a JsonSerializer instance too -------

    [Fact]
    public void SerializerInstance_HonorsResolver()
    {
        var serializer = JsonSerializer.Create(Settings());
        var poco = new RenamedPoco { FirstName = "Ada" };

        using var sw = new StringWriter();
        serializer.Serialize(sw, poco);
        var obj = JObject.Parse(sw.ToString());

        Assert.Equal("Ada", (string?)obj["first_name"]);
    }
}
