using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace ES.FX.TransactionalOutbox.Serialization;

/// <summary>
///     Interface for serializing and deserializing outbox payloads and headers.
/// </summary>
[PublicAPI]
public interface IOutboxSerializer
{
    /// <summary>
    ///     Serializes the payload and headers for storage in the outbox.
    /// </summary>
    /// <param name="payload">The payload to serialize</param>
    /// <param name="type">The type of the payload</param>
    /// <param name="headers">
    ///     The headers associated with the payload. If a non-null dictionary is supplied, it may be augmented in place
    ///     by the configured <see cref="IPayloadTypeProvider" /> (via
    ///     <see cref="IPayloadTypeProvider.SetTypeHeaders" />) before serialization.
    /// </param>
    /// <param name="payloadType">The string representation of the payload type</param>
    /// <param name="serializedPayload">The serialized payload</param>
    /// <param name="serializedHeaders">The serialized headers, or <c>null</c> if there are no headers</param>
    [RequiresUnreferencedCode(
        "Serialization of the payload might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode(
        "Serialization of the payload might require dynamic code generation.")]
    void Serialize(object payload, Type type, IDictionary<string, string>? headers,
        out string payloadType, out string serializedPayload, out string? serializedHeaders);

    /// <summary>
    ///     Deserializes the payload and headers previously stored in the outbox.
    /// </summary>
    /// <param name="serializedPayload">The serialized payload</param>
    /// <param name="payloadType">The string representation of the payload type</param>
    /// <param name="serializedHeaders">The serialized headers, or <c>null</c> if there are no headers</param>
    /// <param name="payload">The deserialized payload</param>
    /// <param name="type">The resolved type of the payload</param>
    /// <param name="headers">The deserialized headers</param>
    [RequiresUnreferencedCode(
        "Deserialization of the payload might require types that cannot be statically analyzed.")]
    [RequiresDynamicCode(
        "Deserialization of the payload might require dynamic code generation.")]
    void Deserialize(string serializedPayload, string payloadType, string? serializedHeaders,
        out object payload, out Type type, out Dictionary<string, string> headers);
}