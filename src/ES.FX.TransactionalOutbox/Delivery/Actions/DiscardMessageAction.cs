namespace ES.FX.TransactionalOutbox.Delivery.Actions;

/// <summary>
///     Action indicating that the outbox message should be discarded and not delivered again.
/// </summary>
public sealed class DiscardMessageAction : IMessageAction
{
    internal DiscardMessageAction()
    {
    }
}