namespace ES.FX.TransactionalOutbox.Delivery;

/// <summary>
///     Options for delivering the outbox message
/// </summary>
public class OutboxMessageDeliveryOptions
{
    /// <summary>
    ///     The time at which the message should be delivered. If this is null, the message will be delivered immediately
    /// </summary>
    public DateTimeOffset? NotBefore { get; set; }

    /// <summary>
    ///     The time at which the message should not be delivered after. If the message is not delivered by this time, it will
    ///     be discarded
    /// </summary>
    public DateTimeOffset? NotAfter { get; set; }
}