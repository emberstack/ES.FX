using System.Collections.Concurrent;
using System.Reflection;
using JetBrains.Annotations;

namespace ES.FX.ComponentModel.DataAnnotations;

/// <summary>
///     Attribute used to specify a custom type for a fault payload.
///     This can be used to override the default type or for lookups during serialization/deserialization
/// </summary>
/// <param name="type">The custom type for the payload</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class FaultPayloadTypeAttribute(string type) : Attribute
{
    /// <summary>
    ///     Keeps a cache of the attributes for each type
    /// </summary>
    private static readonly ConcurrentDictionary<Type, FaultPayloadTypeAttribute?> TypeAttributes = new();

    /// <summary>
    ///     The custom type for the payload
    /// </summary>
    public string PayloadType { get; } = type;

    /// <summary>
    ///     Gets the custom payload type for the specified type
    /// </summary>
    /// <param name="type">The type to get the custom payload type for</param>
    /// <returns>The custom payload type</returns>
    public static string? PayloadTypeFor(Type type)
    {
        if (TypeAttributes.TryGetValue(type, out var attribute)) return attribute?.PayloadType;
        attribute = type.GetCustomAttribute<FaultPayloadTypeAttribute>();
        TypeAttributes.TryAdd(type, attribute);
        return attribute?.PayloadType;
    }

    /// <summary>
    ///     Gets the custom payload type for the specified type
    /// </summary>
    /// <typeparam name="T">The type to get the custom payload type for</typeparam>
    /// <returns>The custom payload type</returns>
    [PublicAPI]
    public static string? PayloadTypeFor<T>() => PayloadTypeFor(typeof(T));
}