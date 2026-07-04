using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ES.FX.TransactionalOutbox.Serialization;

namespace ES.FX.TransactionalOutbox.Tests;

public class DefaultOutboxSerializerTests
{
    private static DefaultOutboxSerializer CreateSerializer(JsonSerializerOptions? options = null) =>
        new(new DefaultPayloadTypeProvider(), options);

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test uses reflection-based JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test uses reflection-based JSON.")]
    public void Serialize_Then_Deserialize_RoundTrips_Payload()
    {
        var serializer = CreateSerializer();
        var original = new SamplePayload(42, "outbox", new[] { "a", "b" });

        serializer.Serialize(original, typeof(SamplePayload), null,
            out var payloadType, out var serializedPayload, out var serializedHeaders);

        // Payload type is the assembly-qualified name of the concrete type.
        Assert.Equal(typeof(SamplePayload).AssemblyQualifiedName, payloadType);
        Assert.False(string.IsNullOrWhiteSpace(serializedPayload));
        // Default provider adds no type headers, so headers stay empty => null.
        Assert.Null(serializedHeaders);

        serializer.Deserialize(serializedPayload, payloadType, serializedHeaders,
            out var payload, out var type, out var headers);

        Assert.Equal(typeof(SamplePayload), type);
        var roundTripped = Assert.IsType<SamplePayload>(payload);
        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.Name, roundTripped.Name);
        Assert.Equal(original.Tags, roundTripped.Tags);
        Assert.Empty(headers);
    }

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test uses reflection-based JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test uses reflection-based JSON.")]
    public void Serialize_With_Headers_RoundTrips_Headers()
    {
        var serializer = CreateSerializer();
        var payload = new SamplePayload(1, "x", Array.Empty<string>());
        var headers = new Dictionary<string, string>
        {
            ["correlation-id"] = "abc-123",
            ["source"] = "unit-test"
        };

        serializer.Serialize(payload, typeof(SamplePayload), headers,
            out _, out var serializedPayload, out var serializedHeaders);

        Assert.NotNull(serializedHeaders);

        serializer.Deserialize(serializedPayload, typeof(SamplePayload).AssemblyQualifiedName!, serializedHeaders,
            out _, out _, out var deserializedHeaders);

        Assert.Equal("abc-123", deserializedHeaders["correlation-id"]);
        Assert.Equal("unit-test", deserializedHeaders["source"]);
        Assert.Equal(2, deserializedHeaders.Count);
    }

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test uses reflection-based JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test uses reflection-based JSON.")]
    public void Serialize_Uses_Declared_Type_For_Payload_Type()
    {
        var serializer = CreateSerializer();
        var payload = new SamplePayload(7, "declared", Array.Empty<string>());

        // Serialize against object as the declared type; payloadType reflects the passed 'type' argument.
        serializer.Serialize(payload, typeof(object), null,
            out var payloadType, out _, out _);

        Assert.Equal(typeof(object).AssemblyQualifiedName, payloadType);
    }

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test uses reflection-based JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test uses reflection-based JSON.")]
    public void Deserialize_With_Null_Headers_Returns_Empty_Dictionary()
    {
        var serializer = CreateSerializer();
        var payload = new SamplePayload(3, "y", Array.Empty<string>());
        serializer.Serialize(payload, typeof(SamplePayload), null,
            out var payloadType, out var serializedPayload, out _);

        serializer.Deserialize(serializedPayload, payloadType, null,
            out _, out _, out var headers);

        Assert.NotNull(headers);
        Assert.Empty(headers);
    }

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test uses reflection-based JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test uses reflection-based JSON.")]
    public void Serialize_Honors_Custom_JsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        var serializer = CreateSerializer(options);
        var payload = new SamplePayload(9, "casing", Array.Empty<string>());

        serializer.Serialize(payload, typeof(SamplePayload), null,
            out _, out var serializedPayload, out _);

        // camelCase policy => property names start lowercase.
        Assert.Contains("\"id\"", serializedPayload);
        Assert.Contains("\"name\"", serializedPayload);
        Assert.DoesNotContain("\"Id\"", serializedPayload);
    }

    [Fact]
    public void Constructor_Throws_When_TypeProvider_Is_Null() =>
        Assert.Throws<ArgumentNullException>(() => new DefaultOutboxSerializer(null!));

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test uses reflection-based JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test uses reflection-based JSON.")]
    public void Deserialize_Throws_NotSupported_When_Payload_Deserializes_To_Null()
    {
        var serializer = CreateSerializer();

        // A literal JSON "null" makes JsonSerializer.Deserialize return null, which trips the guard.
        var ex = Assert.Throws<NotSupportedException>(() =>
            serializer.Deserialize("null", typeof(SamplePayload).AssemblyQualifiedName!, null,
                out _, out _, out _));

        Assert.Equal("Could not deserialize message", ex.Message);
    }

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test uses reflection-based JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test uses reflection-based JSON.")]
    public void Serialize_Lets_Custom_Provider_Inject_Type_Header_That_Drives_Deserialization()
    {
        var serializer = new DefaultOutboxSerializer(new HeaderDrivenTypeProvider());
        var original = new SamplePayload(11, "header-driven", new[] { "t1" });

        serializer.Serialize(original, typeof(SamplePayload), null,
            out var payloadType, out var serializedPayload, out var serializedHeaders);

        // The provider returns an opaque token instead of the AQN, proving type resolution now relies on headers.
        Assert.Equal("opaque-token", payloadType);
        // SetTypeHeaders mutated the (initially empty) headers dictionary in place, so headers were serialized.
        Assert.NotNull(serializedHeaders);
        Assert.Contains(HeaderDrivenTypeProvider.TypeHeaderKey, serializedHeaders);

        serializer.Deserialize(serializedPayload, payloadType, serializedHeaders,
            out var payload, out var type, out var headers);

        // GetType resolved the concrete type solely from the injected header that survived the round-trip.
        Assert.Equal(typeof(SamplePayload), type);
        Assert.Equal(typeof(SamplePayload).AssemblyQualifiedName, headers[HeaderDrivenTypeProvider.TypeHeaderKey]);
        var roundTripped = Assert.IsType<SamplePayload>(payload);
        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.Name, roundTripped.Name);
    }

    [Fact]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test uses reflection-based JSON.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test uses reflection-based JSON.")]
    public void Serialize_Custom_Provider_Augments_Caller_Supplied_Headers_In_Place()
    {
        var serializer = new DefaultOutboxSerializer(new HeaderDrivenTypeProvider());
        var payload = new SamplePayload(2, "merge", Array.Empty<string>());
        var headers = new Dictionary<string, string> { ["correlation-id"] = "abc-123" };

        serializer.Serialize(payload, typeof(SamplePayload), headers,
            out _, out _, out var serializedHeaders);

        Assert.NotNull(serializedHeaders);

        // Only the headers survive the round-trip here; the payload body is irrelevant to this assertion.
        serializer.Deserialize("{}", "opaque-token", serializedHeaders,
            out _, out _, out var deserializedHeaders);

        // Caller header is preserved AND the provider's injected type header is present alongside it.
        Assert.Equal("abc-123", deserializedHeaders["correlation-id"]);
        Assert.Equal(typeof(SamplePayload).AssemblyQualifiedName,
            deserializedHeaders[HeaderDrivenTypeProvider.TypeHeaderKey]);
        Assert.Equal(2, deserializedHeaders.Count);
    }

    [Fact]
    public void Protected_Accessors_Are_Reachable_By_Derived_Class()
    {
        var provider = new DefaultPayloadTypeProvider();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var derived = new DerivedSerializer(provider, options);

        Assert.Same(provider, derived.ExposedTypeProvider);
        Assert.Same(options, derived.ExposedOptions);
    }

    [Fact]
    public void Protected_Options_Accessor_Defaults_When_Options_Not_Supplied()
    {
        var derived = new DerivedSerializer(new DefaultPayloadTypeProvider());

        // A default options instance is used (non-null) when none is supplied.
        Assert.NotNull(derived.ExposedOptions);
    }

    public sealed record SamplePayload(int Id, string Name, IReadOnlyList<string> Tags);

    // Custom provider that both round-trips a type via an injected header and augments headers in place
    // during Serialize. Proves the SetTypeHeaders/GetType contract drives type resolution end to end.
    private sealed class HeaderDrivenTypeProvider : IPayloadTypeProvider
    {
        public const string TypeHeaderKey = "x-payload-type";

        public string GetPayloadType(Type payloadType) => "opaque-token";

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test resolves types via reflection.")]
        public Type GetType(string payloadType, IReadOnlyDictionary<string, string> headers)
        {
            // Ignore the opaque payloadType token; resolve strictly from the header injected during Serialize.
            var aqn = headers[TypeHeaderKey];
            return Type.GetType(aqn) ?? throw new InvalidOperationException($"Type '{aqn}' not found.");
        }

        public void SetTypeHeaders(Type payloadType, IDictionary<string, string> headers) =>
            headers[TypeHeaderKey] = payloadType.AssemblyQualifiedName!;
    }

    // Derived type proving the protected TypeProvider/Options accessors are reachable by subclasses.
    private sealed class DerivedSerializer(IPayloadTypeProvider typeProvider, JsonSerializerOptions? options = null)
        : DefaultOutboxSerializer(typeProvider, options)
    {
        public IPayloadTypeProvider ExposedTypeProvider => TypeProvider;
        public JsonSerializerOptions ExposedOptions => Options;
    }
}