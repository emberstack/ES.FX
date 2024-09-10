using System.Collections.Concurrent;
using JetBrains.Annotations;

namespace ES.FX.Contracts.Messaging;

/// <summary>
///     Attribute used to specify a custom type for a fault message.
/// </summary>
/// <remarks>
/// </remarks>
/// <param name="type">The type to use for the message</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
[PublicAPI]
public class FaultMessageTypeAttribute(string type) : Attribute
{
    private static readonly ConcurrentDictionary<Type, FaultMessageTypeAttribute?> TypeAttributes = new();

    public string Type { get; } = type;

    public static string? TypeFor(Type type)
    {
        if (TypeAttributes.TryGetValue(type, out var attribute))
            return attribute?.Type;

        attribute = type.GetCustomAttributes(typeof(FaultMessageTypeAttribute), true).Union(
                type.GetInterfaces()
                    .SelectMany(interfaceType =>
                        interfaceType.GetCustomAttributes(typeof(FaultMessageTypeAttribute), true)))
            .Distinct().Cast<FaultMessageTypeAttribute>().FirstOrDefault();

        TypeAttributes.TryAdd(type, attribute);
        return attribute?.Type;
    }
}