using System.Collections.Concurrent;
using ES.FX.ComponentModel.DataAnnotations;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Messages;

internal static class OutboxPayloadTypeProvider
{
    private static readonly ConcurrentDictionary<string, Type?> PayloadTypeDictionary = new();


    internal static Type? GetMessageTypeByPayloadType(string payloadType, string? assemblyQualifiedName)
    {
        if (PayloadTypeDictionary.TryGetValue(payloadType, out var type)) return type;
        type = Type.GetType(payloadType) ??
               Type.GetType(assemblyQualifiedName ?? string.Empty);
        PayloadTypeDictionary.TryAdd(payloadType, type);
        return type;
    }

    internal static string GetPayloadType(Type type) =>
        PayloadTypeAttribute.PayloadTypeFor(type) ?? type.AssemblyQualifiedName!;

    internal static void RegisterTypes(params Type[] messageTypes)
    {
        foreach (var type in messageTypes) PayloadTypeDictionary.TryAdd(GetPayloadType(type), type);
    }
}