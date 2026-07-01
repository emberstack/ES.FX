namespace ES.FX.TransactionalOutbox.Delivery.Faults;

/// <summary>
///     Context for an outbox message delivery fault handled by an <see cref="IOutboxMessageFaultHandler" />.
/// </summary>
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