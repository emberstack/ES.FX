using System.Collections.Concurrent;
using System.Reflection;
using JetBrains.Annotations;

namespace ES.FX.Contracts.Payloads;

/// <summary>
///     Attribute used to specify a custom type for a fault payload.
///     This can be used to override the default type or for lookups during serialization/deserialization
/// </summary>
/// <param name="type">The custom type for the payload</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class FaultPayloadTypeAttribute(string type) : Attribute
{
    private static readonly ConcurrentDictionary<Type, FaultPayloadTypeAttribute?> TypeAttributes = new();

    public string PayloadType { get; } = type;

    public static string? PayloadTypeFor(Type type)
    {
        if (TypeAttributes.TryGetValue(type, out var attribute)) return attribute?.PayloadType;
        attribute = type.GetCustomAttribute<FaultPayloadTypeAttribute>();
        TypeAttributes.TryAdd(type, attribute);
        return attribute?.PayloadType;
    }

    [PublicAPI]
    public static string? PayloadTypeFor<T>() => PayloadTypeFor(typeof(T));
}