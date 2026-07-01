using System.Diagnostics.CodeAnalysis;

namespace ES.FX.TransactionalOutbox.Serialization;

/// <summary>
///     Provides the mapping between payload <see cref="Type" />s and their string representation stored in the outbox.
///     The default implementation is <see cref="DefaultPayloadTypeProvider" />.
/// </summary>
public interface IPayloadTypeProvider
{
    /// <summary>
    ///     Resolves the type of the payload based on the provided payload type string and headers.
    /// </summary>
    /// <param name="payloadType">The string representation of the payload type</param>
    /// <param name="headers">The headers associated with the payload</param>
    /// <returns>The resolved payload <see cref="Type" /></returns>
    [RequiresUnreferencedCode(
        "Resolving the payload type might require types that cannot be statically analyzed.")]
    public Type GetType(string payloadType, IReadOnlyDictionary<string, string> headers);


    /// <summary>
    ///     Gets the type of the payload. Default implementation uses the assembly qualified name of the type.
    /// </summary>
    public string GetPayloadType(Type payloadType) => payloadType.AssemblyQualifiedName ??
                                                      throw new InvalidOperationException(
                                                          $"Type '{payloadType.FullName}' does not have an assembly qualified name.");

    /// <summary>
    ///     Allows setting headers for a specific type. Implementations may add entries to the supplied
    ///     <paramref name="headers" /> dictionary, which is mutated in place. The default implementation does nothing.
    /// </summary>
    /// <param name="payloadType">The type of the payload</param>
    /// <param name="headers">The headers dictionary to augment</param>
    public void SetTypeHeaders(Type payloadType, IDictionary<string, string> headers)
    {
    }
}