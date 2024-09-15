using System.Collections.Concurrent;
using ES.FX.Contracts.Messaging;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.MassTransit.Serialization;

/// <summary>
///     Provider that reads the message type from the transport headers and maps it to the corresponding .NET type
/// </summary>
[PublicAPI]
public static class MassTransitMessageTypeProvider
{
    public const string Header = "X-ES-FX-MassTransit-MessageType";

    private static readonly ConcurrentDictionary<string, Type> TypeMap = new();

    public static Type? GetType(string? messageType) =>
        messageType is null ? null : TypeMap.GetValueOrDefault(messageType);


    public static Type? GetType(ReceiveContext receiveContext) =>
        !receiveContext.TransportHeaders.TryGetHeader(Header, out var headerValue)
            ? null
            : GetType(headerValue.ToString());

    public static void RegisterTypes(params Type[] messageTypes)
    {
        foreach (var type in messageTypes)
        {
            var messageType = MessageTypeAttribute.MessageTypeFor(type);
            if (messageType is not null)
                TypeMap.TryAdd(messageType, type);
        }
    }
}