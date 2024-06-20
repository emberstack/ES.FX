using System.Collections.Concurrent;
using ES.FX.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.Additions.MassTransit.Serialization;

/// <summary>
///     Provider that reads the payload type from the transport headers and maps it to the corresponding .NET type
/// </summary>
[PublicAPI]
public static class MassTransitPayloadTypeProvider
{
    public const string Header = $"X-{nameof(ES)}-{nameof(FX)}-{nameof(MassTransit)}-PayloadType";

    private static readonly ConcurrentDictionary<string, Type> TypeMap = new();

    public static Type? GetType(string? payloadType) =>
        payloadType is null ? null : TypeMap.GetValueOrDefault(payloadType);


    public static Type? GetType(ReceiveContext receiveContext) =>
        !receiveContext.TransportHeaders.TryGetHeader(Header, out var headerValue)
            ? null
            : GetType(headerValue.ToString());

    public static void RegisterTypes(params Type[] types)
    {
        foreach (var type in types)
        {
            var payloadType = PayloadTypeAttribute.PayloadTypeFor(type);
            if (payloadType is not null)
                TypeMap.TryAdd(payloadType, type);
        }
    }
}