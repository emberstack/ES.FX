namespace ES.FX.TransactionalOutbox.Delivery.Faults;

public record OutboxMessageFaultContext
{
    /// <summary>
    ///     The context of the outbox message that has encountered a fault.
    /// </summary>
    public required OutboxMessageContext MessageContext { get; init; }

    /// <summary>
    ///     The exception that caused the fault.
    /// </summary>
    public required Exception FaultException { get; init; }
}