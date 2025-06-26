using System.Text.Json;

namespace ES.FX.TransactionalOutbox.Serialization;

/// <summary>
///     Default implementation of <see cref="IOutboxSerializer" /> that uses JSON serialization
/// </summary>
public class DefaultOutboxSerializer(IPayloadTypeProvider typeProvider) : IOutboxSerializer
{
    public virtual string SerializePayload(object payload, Type payloadType, out string payloadTypeString)
    {
        payloadTypeString = typeProvider.SetType(payloadType);
        return JsonSerializer.Serialize(payload, payloadType);
    }

    public virtual object DeserializePayload(string payload, string payloadType, Dictionary<string, string> headers)
    {
        var type = typeProvider.GetType(payloadType, headers);
        return JsonSerializer.Deserialize(payload, type) ??
               throw new NotSupportedException("Could not deserialize message");
    }

    public virtual string? SerializeHeaders(IDictionary<string, string>? headers, Type payloadType)
    {
        headers ??= new Dictionary<string, string>();
        typeProvider.SetTypeHeaders(payloadType, headers);
        return headers.Count == 0 ? null : JsonSerializer.Serialize(headers);
    }

    public virtual Dictionary<string, string> DeserializeHeaders(string? serializedHeaders) =>
        string.IsNullOrWhiteSpace(serializedHeaders)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(serializedHeaders)!;
}