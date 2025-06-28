namespace ES.FX.TransactionalOutbox.Delivery;

public record OutboxMessageContext
{
    /// <summary>
    ///     Represents the type of the message being delivered.
    /// </summary>
    public required Type MessageType { get; init; }

    /// <summary>
    ///     Represents the message being delivered.
    /// </summary>
    public required object Message { get; init; }

    /// <summary>
    ///     Represents the headers associated with the message being delivered.
    /// </summary>
    public IDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();


    /// <summary>
    ///     Gets the number of delivery attempts made for the message.
    /// </summary>
    public required int DeliveryAttempts { get; init; }

    /// <summary>
    ///     Gets the time at which the message was first attempted to be delivered.
    /// </summary>
    public required DateTimeOffset DeliveryFirstAttemptedAt { get; init; }

    /// <summary>
    ///     Gets the time at which the message was last attempted to be delivered.
    /// </summary>
    public required DateTimeOffset? DeliveryLastAttemptedAt { get; init; }
}