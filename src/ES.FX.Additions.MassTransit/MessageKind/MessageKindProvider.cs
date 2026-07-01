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
    /// <summary>
    ///     The transport header used to carry the kind of the message
    /// </summary>
    public const string Header = $"X-{nameof(ES)}-{nameof(FX)}-Kind";

    private static readonly ConcurrentDictionary<string, Type> Cache = new();

    /// <summary>
    ///     Gets the registered .NET type for the specified kind
    /// </summary>
    /// <param name="kind">The kind to resolve</param>
    /// <returns>The registered type, or null if the kind is null or not registered</returns>
    public static Type? GetType(string? kind) =>
        kind is null ? null : Cache.GetValueOrDefault(kind);


    /// <summary>
    ///     Gets the registered .NET type for the kind read from the transport headers of the specified
    ///     <see cref="ReceiveContext" />
    /// </summary>
    /// <param name="receiveContext">The receive context to read the kind header from</param>
    /// <returns>The registered type, or null if the header is missing or the kind is not registered</returns>
    public static Type? GetType(ReceiveContext receiveContext) =>
        !receiveContext.TransportHeaders.TryGetHeader(Header, out var headerValue)
            ? null
            : GetType(headerValue.ToString());

    /// <summary>
    ///     Registers the specified types by their <see cref="KindAttribute" /> kind. Types without a kind are ignored.
    ///     If a kind is already registered, the first registration wins and later ones are ignored.
    /// </summary>
    /// <param name="types">The types to register</param>
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