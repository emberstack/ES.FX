using System.Collections.Concurrent;
using System.Reflection;
using JetBrains.Annotations;

namespace ES.FX.Contracts.Messaging;

/// <summary>
///     Attribute used to specify a custom type for a fault message.
/// </summary>
/// <remarks>
/// </remarks>
/// <param name="messageType">The type to use for the message</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
[PublicAPI]
public class FaultMessageTypeAttribute(string messageType) : Attribute
{
    private static readonly ConcurrentDictionary<Type, FaultMessageTypeAttribute?> TypeAttributes = new();

    public string MessageType { get; } = messageType;

    public static string? MessageTypeFor(Type type)
    {
        if (TypeAttributes.TryGetValue(type, out var attribute)) return attribute?.MessageType;
        attribute = type.GetCustomAttribute<FaultMessageTypeAttribute>();
        TypeAttributes.TryAdd(type, attribute);
        return attribute?.MessageType;
    }

    public static string? MessageTypeFor<T>() =>
        MessageTypeFor(typeof(T));
}