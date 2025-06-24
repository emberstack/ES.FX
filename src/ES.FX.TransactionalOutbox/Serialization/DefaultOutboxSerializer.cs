using System.Text.Json;

namespace ES.FX.TransactionalOutbox.Serialization;

/// <summary>
///     Default implementation of <see cref="IOutboxSerializer" /> that uses JSON serialization
/// </summary>
public class DefaultOutboxSerializer : IOutboxSerializer
{
    public string SerializePayload(object payload, Type payloadType) => JsonSerializer.Serialize(payload, payloadType);

    public object DeserializePayload(string payload, string payloadType, Dictionary<string, string> headers) =>
        JsonSerializer.Deserialize(payload,
            Type.GetType(payloadType) ?? throw new InvalidOperationException($"Type '{payloadType}' not found.")) ??
        throw new NotSupportedException("Could not deserialize message");

    public string? SerializeHeaders(IDictionary<string, string>? headers) => headers is null || headers.Count == 0
        ? null
        : JsonSerializer.Serialize(headers);

    public Dictionary<string, string> DeserializeHeaders(string? serializedHeaders) =>
        string.IsNullOrWhiteSpace(serializedHeaders)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(serializedHeaders)!;
}