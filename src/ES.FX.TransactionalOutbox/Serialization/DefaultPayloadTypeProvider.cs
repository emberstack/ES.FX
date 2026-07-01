using System.Diagnostics.CodeAnalysis;

namespace ES.FX.TransactionalOutbox.Serialization;

/// <summary>
///     Default implementation of <see cref="IPayloadTypeProvider" /> that resolves payload types from their
///     assembly-qualified name.
/// </summary>
public class DefaultPayloadTypeProvider : IPayloadTypeProvider
{
    [RequiresUnreferencedCode(
        "Resolving the payload type from its assembly-qualified name uses reflection and might require types that cannot be statically analyzed.")]
    public Type GetType(string payloadType, IReadOnlyDictionary<string, string> headers) => Type.GetType(payloadType) ??
        throw new InvalidOperationException($"Type '{payloadType}' not found.");
}