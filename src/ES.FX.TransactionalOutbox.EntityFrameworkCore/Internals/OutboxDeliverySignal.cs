using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Internals;

/// <summary>
///     Signal used to deliver outbox messages
/// </summary>
internal static class OutboxDeliverySignal
{
    private static readonly ConcurrentDictionary<Type, Channel<string>> Channels = new();

    public static Channel<string> GetChannel(Type type) =>
        Channels.GetOrAdd(type, _ => Channel.CreateUnbounded<string>());

    public static Channel<string> GetChannel<TType>() => GetChannel(typeof(TType));
}