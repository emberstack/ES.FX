using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace ES.FX.TransactionalOutbox.Serialization;

/// <summary>
///     Default implementation of <see cref="IOutboxSerializer" /> that uses JSON serialization
/// </summary>
public class DefaultOutboxSerializer(IPayloadTypeProvider typeProvider, JsonSerializerOptions? options = null)
    : IOutboxSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.General);

    /// <summary>
    ///     The <see cref="IPayloadTypeProvider" /> used to resolve payload types and their string representation.
    ///     Exposed so derived classes overriding <see cref="Serialize" /> or <see cref="Deserialize" /> can reuse the
    ///     configured provider.
    /// </summary>
    protected IPayloadTypeProvider TypeProvider { get; } =
        typeProvider ?? throw new ArgumentNullException(nameof(typeProvider));

    /// <summary>
    ///     The <see cref="JsonSerializerOptions" /> used for JSON serialization and deserialization.
    ///     Exposed so derived classes overriding <see cref="Serialize" /> or <see cref="Deserialize" /> can reuse the
    ///     configured options.
    /// </summary>
    protected JsonSerializerOptions Options { get; } = options ?? DefaultOptions;

    [RequiresUnreferencedCode(
        "JSON serialization of the payload uses reflection and might require types that cannot be statically analyzed. Supply a source-generated JsonSerializerOptions to be trim-safe.")]
    [RequiresDynamicCode(
        "JSON serialization of the payload uses reflection and might require dynamic code generation. Supply a source-generated JsonSerializerOptions to be AOT-safe.")]
    public virtual void Serialize(object payload, Type type, IDictionary<string, string>? headers,
        out string payloadType, out string serializedPayload, out string? serializedHeaders)
    {
        headers ??= new Dictionary<string, string>();
        TypeProvider.SetTypeHeaders(type, headers);
        serializedHeaders = headers.Count == 0 ? null : JsonSerializer.Serialize(headers, Options);

        payloadType = TypeProvider.GetPayloadType(type);
        serializedPayload = JsonSerializer.Serialize(payload, type, Options);
    }

    [RequiresUnreferencedCode(
        "JSON deserialization of the payload uses reflection and might require types that cannot be statically analyzed. Supply a source-generated JsonSerializerOptions to be trim-safe.")]
    [RequiresDynamicCode(
        "JSON deserialization of the payload uses reflection and might require dynamic code generation. Supply a source-generated JsonSerializerOptions to be AOT-safe.")]
    public virtual void Deserialize(string serializedPayload, string payloadType, string? serializedHeaders,
        out object payload, out Type type, out Dictionary<string, string> headers)
    {
        headers = string.IsNullOrWhiteSpace(serializedHeaders)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(serializedHeaders, Options)!;

        type = TypeProvider.GetType(payloadType, headers);
        payload = JsonSerializer.Deserialize(serializedPayload, type, Options) ??
                  throw new NotSupportedException("Could not deserialize message");
    }
}