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
/// <param name="messageType">The name to use for the message</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
[PublicAPI]
public class MessageTypeAttribute(string messageType) : Attribute
{
    private static readonly ConcurrentDictionary<Type, MessageTypeAttribute?> TypeAttributes = new();

    public string MessageType { get; } = messageType;

    public static string? MessageTypeFor(Type type)
    {
        if (TypeAttributes.TryGetValue(type, out var attribute)) return attribute?.MessageType;
        attribute = type.GetCustomAttribute<MessageTypeAttribute>();
        TypeAttributes.TryAdd(type, attribute);
        return attribute?.MessageType;
    }

    public static string? MessageTypeFor<T>() =>
        MessageTypeFor(typeof(T));
}