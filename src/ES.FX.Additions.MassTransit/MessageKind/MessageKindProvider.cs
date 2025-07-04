using System.Collections.Concurrent;
using ES.FX.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.Additions.MassTransit.MessageKind;

/// <summary>
///     Provider that reads the kind for a message from the transport headers and maps it to the corresponding .NET type
/// </summary>
[PublicAPI]
public static class MessageKindProvider
{
    public const string Header = $"X-{nameof(ES)}-{nameof(FX)}-Kind";

    private static readonly ConcurrentDictionary<string, Type> Cache = new();

    public static Type? GetType(string? kind) =>
        kind is null ? null : Cache.GetValueOrDefault(kind);


    public static Type? GetType(ReceiveContext receiveContext) =>
        !receiveContext.TransportHeaders.TryGetHeader(Header, out var headerValue)
            ? null
            : GetType(headerValue.ToString());

    public static void RegisterTypes(params Type[] types)
    {
        foreach (var type in types)
        {
            var kind = KindAttribute.For(type);
            if (kind is not null)
                Cache.TryAdd(kind, type);
        }
    }
}