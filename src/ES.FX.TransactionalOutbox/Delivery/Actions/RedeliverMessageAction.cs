namespace ES.FX.TransactionalOutbox.Delivery.Actions;

public sealed class RedeliverMessageAction : IMessageAction
{
    internal RedeliverMessageAction(TimeSpan delay) => Delay = delay;
    public TimeSpan Delay { get; }
}