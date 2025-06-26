using JetBrains.Annotations;

namespace ES.FX.TransactionalOutbox.Serialization;

/// <summary>
///     Interface for serializing and deserializing outbox payloads and headers.
/// </summary>
[PublicAPI]
public interface IOutboxSerializer
{
    public string SerializePayload(object payload, Type payloadType, out string payloadTypeString);

    public object DeserializePayload(string payload, string payloadType, Dictionary<string, string> headers);

    public string? SerializeHeaders(IDictionary<string, string>? headers, Type payloadType);

    public Dictionary<string, string> DeserializeHeaders(string? serializedHeaders);
}