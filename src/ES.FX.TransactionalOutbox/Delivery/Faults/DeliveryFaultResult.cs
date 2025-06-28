using ES.FX.TransactionalOutbox.Delivery.Actions;

namespace ES.FX.TransactionalOutbox.Delivery.Faults;

public sealed class DeliveryFaultResult
{
    private DeliveryFaultResult(IMessageAction result)
        => Action = result;

    /// <summary>
    ///     The action to take for the message that has failed delivery.
    /// </summary>
    public IMessageAction Action { get; }

    /// <summary>
    ///     Discard the message.
    /// </summary>
    public static DeliveryFaultResult Discard() => new(new DiscardMessageAction());

    /// <summary>
    ///     Request redelivery, optionally after <paramref name="delay" />.
    /// </summary>
    public static DeliveryFaultResult Redeliver(TimeSpan delay) => new(new RedeliverMessageAction(delay));
}