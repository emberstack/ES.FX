using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Internals;

/// <summary>
///     Signal used to deliver outbox messages
/// </summary>
internal static class OutboxDeliverySignal
{
    private static readonly ConcurrentDictionary<Type, Channel<string>> Channels = new();

    public static Channel<string> GetChannel(Type type)
    {
        Channel.CreateUnbounded<string>(new UnboundedChannelOptions());
        return Channels.GetOrAdd(type, _ => Channel.CreateUnbounded<string>());
    }

    public static Channel<string> GetChannel<TType>() => GetChannel(typeof(TType));


    public static Channel<string> RenewChannel(Type type)
    {
        var channel = Channel.CreateUnbounded<string>();
        return Channels.AddOrUpdate(type, _ => channel, (_, _) => channel);
    }

    public static Channel<string> RenewChannel<TType>() => RenewChannel(typeof(TType));
}