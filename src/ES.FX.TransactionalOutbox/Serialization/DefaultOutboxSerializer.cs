using System.Text.Json;

namespace ES.FX.TransactionalOutbox.Serialization;

/// <summary>
///     Default implementation of <see cref="IOutboxSerializer" /> that uses JSON serialization
/// </summary>
public class DefaultOutboxSerializer(IPayloadTypeProvider typeProvider) : IOutboxSerializer
{
    public virtual void Serialize(object payload, Type type, IDictionary<string, string>? headers,
        out string payloadType, out string serializedPayload, out string? serializedHeaders)
    {
        headers ??= new Dictionary<string, string>();
        typeProvider.SetTypeHeaders(type, headers);
        serializedHeaders = headers.Count == 0 ? null : JsonSerializer.Serialize(headers);

        payloadType = typeProvider.GetPayloadType(type);
        serializedPayload = JsonSerializer.Serialize(payload, type);
    }

    public virtual void Deserialize(string serializedPayload, string payloadType, string? serializedHeaders,
        out object payload, out Type type, out Dictionary<string, string> headers)
    {
        headers = string.IsNullOrWhiteSpace(serializedHeaders)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(serializedHeaders)!;

        type = typeProvider.GetType(payloadType, headers);
        payload = JsonSerializer.Deserialize(serializedPayload, type) ??
                  throw new NotSupportedException("Could not deserialize message");
    }
}