namespace ES.FX.TransactionalOutbox.Delivery.Faults;

public interface IOutboxMessageFaultHandler
{
    /// <summary>
    ///     Handles the outbox message fault.
    /// </summary>
    ValueTask<DeliveryFaultResult> HandleAsync(OutboxMessageFaultContext context,
        CancellationToken cancellationToken = default);
}