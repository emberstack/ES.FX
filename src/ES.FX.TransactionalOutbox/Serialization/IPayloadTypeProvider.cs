namespace ES.FX.TransactionalOutbox.Serialization;

public interface IPayloadTypeProvider
{
    /// <summary>
    ///     Resolves the type of the payload based on the provided payload type string and headers.
    /// </summary>
    /// <param name="payloadType"></param>
    /// <param name="headers"></param>
    /// <returns></returns>
    public Type GetType(string payloadType, IReadOnlyDictionary<string, string> headers);


    /// <summary>
    ///     Sets the type of the payload. Default implementation uses the assembly qualified name of the type.
    /// </summary>
    public string SetType(Type payloadType) => payloadType.AssemblyQualifiedName ??
                                               throw new InvalidOperationException(
                                                   $"Type '{payloadType.FullName}' does not have an assembly qualified name.");

    /// <summary>
    ///     Allows setting headers for a specific type.
    /// </summary>
    public void SetTypeHeaders(Type payloadType, IDictionary<string, string> headers)
    {
    }
}