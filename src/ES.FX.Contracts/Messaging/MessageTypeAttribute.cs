using System.Collections.Concurrent;
using System.Reflection;
using JetBrains.Annotations;

namespace ES.FX.Contracts.Messaging;

/// <summary>
///     Attribute used to specify a custom type for a message.
///     This can be used to override the default message type or for lookups during serialization/deserialization
/// </summary>
/// <remarks>
/// </remarks>
/// <param name="type">The name to use for the message</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
[PublicAPI]
public class MessageTypeAttribute(string type) : Attribute
{
    private static readonly ConcurrentDictionary<Type, MessageTypeAttribute?> TypeAttributes = new();

    public string Type { get; } = type;

    public static string? TypeFor(Type type)
    {
        if (TypeAttributes.TryGetValue(type, out var attribute)) return attribute?.Type;
        attribute = type.GetCustomAttribute<MessageTypeAttribute>();
        TypeAttributes.TryAdd(type, attribute);
        return attribute?.Type;
    }
}