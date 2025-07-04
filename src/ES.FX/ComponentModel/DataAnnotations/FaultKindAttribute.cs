using System.Collections.Concurrent;
using System.Reflection;
using JetBrains.Annotations;

namespace ES.FX.ComponentModel.DataAnnotations;

/// <summary>
///     Attribute used to specify the kind of fault.
///     This can be used to override the default type or for lookups during serialization/deserialization
/// </summary>
/// <param name="kind">The object kind</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public class FaultKindAttribute(string kind) : Attribute
{
    /// <summary>
    ///     Keeps a cache of the attributes for each type
    /// </summary>
    private static readonly ConcurrentDictionary<Type, FaultKindAttribute?> TypeCache = new();

    /// <summary>
    ///     The object kind
    /// </summary>
    public string Kind { get; } = kind;

    /// <summary>
    ///     Gets the kind for the specified type
    /// </summary>
    /// <param name="type">The type to get the kind for</param>
    /// <returns>The kind of the type</returns>
    public static string? For(Type type)
    {
        if (TypeCache.TryGetValue(type, out var attribute)) return attribute?.Kind;
        attribute = type.GetCustomAttribute<FaultKindAttribute>();
        TypeCache.TryAdd(type, attribute);
        return attribute?.Kind;
    }

    /// <summary>
    ///     Gets the kind for the specified type
    /// </summary>
    /// <typeparam name="T">The type for which to return the kind</typeparam>
    /// <returns>The kind value</returns>
    [PublicAPI]
    public static string? For<T>() => For(typeof(T));
}