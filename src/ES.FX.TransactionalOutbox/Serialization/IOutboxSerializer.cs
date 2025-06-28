using JetBrains.Annotations;

namespace ES.FX.TransactionalOutbox.Serialization;

/// <summary>
///     Interface for serializing and deserializing outbox payloads and headers.
/// </summary>
[PublicAPI]
public interface IOutboxSerializer
{
    void Serialize(object payload, Type type, IDictionary<string, string>? headers,
        out string payloadType, out string serializedPayload, out string? serializedHeaders);

    void Deserialize(string serializedPayload, string payloadType, string? serializedHeaders,
        out object payload, out Type type, out Dictionary<string, string> headers);
}