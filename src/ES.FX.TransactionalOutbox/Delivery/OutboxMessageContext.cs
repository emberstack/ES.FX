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
}