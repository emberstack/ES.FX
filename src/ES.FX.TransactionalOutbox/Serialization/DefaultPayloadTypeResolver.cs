namespace ES.FX.TransactionalOutbox.Serialization;

public class DefaultPayloadTypeResolver : IPayloadTypeProvider
{
    public Type GetType(string payloadType, IReadOnlyDictionary<string, string> headers) => Type.GetType(payloadType) ??
        throw new InvalidOperationException($"Type '{payloadType}' not found.");
}