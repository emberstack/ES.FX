namespace ES.FX.TransactionalOutbox.Delivery.Actions;

/// <summary>
///     Action indicating that the outbox message should be redelivered after a delay.
/// </summary>
public sealed class RedeliverMessageAction : IMessageAction
{
    internal RedeliverMessageAction(TimeSpan delay) => Delay = delay;

    /// <summary>
    ///     The delay to wait before the message is redelivered.
    /// </summary>
    public TimeSpan Delay { get; }
}