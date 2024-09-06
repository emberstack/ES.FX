using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ES.FX.TransactionalOutbox.EntityFrameworkCore.Delivery;

/// <summary>
///     Signal used to deliver outbox messages
/// </summary>
internal static class OutboxDeliverySignal
{
    private static readonly ConcurrentDictionary<Type, Channel<Type>> Channels = new();

    public static Channel<Type> GetChannel(Type type)
    {
        return Channels.GetOrAdd(type, _ => Channel.CreateUnbounded<Type>());
    }

    public static Channel<Type> GetChannel<TType>() => GetChannel(typeof(TType));


    public static Channel<Type> RenewChannel(Type type)
    {
        var channel = Channel.CreateUnbounded<Type>();
        return Channels.AddOrUpdate(type, _ => channel, (_, _) => channel);
    }

    public static Channel<Type> RenewChannel<TType>() => RenewChannel(typeof(TType));
}