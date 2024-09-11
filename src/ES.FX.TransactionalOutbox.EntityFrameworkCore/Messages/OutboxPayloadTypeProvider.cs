using System.Collections.Concurrent;
using ES.FX.Contracts.Messaging;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Messages;

internal static class OutboxPayloadTypeProvider
{
    private static readonly ConcurrentDictionary<string, Type?> MessageTypeByPayloadTypeDictionary = new();

    internal static string GetPayloadType(Type type) =>
        MessageTypeAttribute.TypeFor(type) ?? type.AssemblyQualifiedName!;

    internal static void RegisterTypes(params Type[] messageTypes)
    {
        foreach (var type in messageTypes) MessageTypeByPayloadTypeDictionary.TryAdd(GetPayloadType(type), type);
    }


    internal static Type? GetMessageTypeByPayloadType(string payloadType, string? assemblyQualifiedName)
    {
        if (MessageTypeByPayloadTypeDictionary.TryGetValue(payloadType, out var type)) return type;
        type = Type.GetType(payloadType) ??
               Type.GetType(assemblyQualifiedName ?? string.Empty);
        MessageTypeByPayloadTypeDictionary.TryAdd(payloadType, type);
        return type;
    }
}