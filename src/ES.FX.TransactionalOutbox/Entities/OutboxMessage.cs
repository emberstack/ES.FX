namespace ES.FX.TransactionalOutbox.Entities;

public class OutboxMessage
{
    /// <summary>
    ///     Message ID. Sequentially generated
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///     Outbox Id
    /// </summary>
    public Guid OutboxId { get; set; }

    /// <summary>
    ///     Time at which the message was added to the outbox
    /// </summary>
    public required DateTimeOffset AddedAt { get; set; }

    /// <summary>
    ///     Serialized header collection. The headers are used to store metadata about the message
    /// </summary>
    public required string? Headers { get; set; }

    /// <summary>
    ///     Serialized message payload. The payload is the actual message that will be delivered
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    ///     Assembly qualified name of the payload type. This is used to deserialize the payload
    /// </summary>
    public required string PayloadType { get; set; }


    /// <summary>
    ///     The activity ID used for diagnostics. This is used to correlate the message with the diagnostics activity
    /// </summary>
    public required string? ActivityId { get; set; }

    /// <summary>
    ///     The number of delivery attempts
    /// </summary>
    public required int DeliveryAttempts { get; set; }

    /// <summary>
    ///     The time at which the message was first attempted to be delivered
    /// </summary>
    public required DateTimeOffset? DeliveryFirstAttemptedAt { get; set; }


    /// <summary>
    ///     The time at which the message was last attempted to be delivered
    /// </summary>
    public required DateTimeOffset? DeliveryLastAttemptedAt { get; set; }

    /// <summary>
    ///     The time after which this message should be delivered. If this is null, the message will be delivered immediately
    /// </summary>
    public required DateTimeOffset? DeliveryNotBefore { get; set; }

    /// <summary>
    ///     The time before which this message should be delivered. If this time is reached, the message will be discarded
    /// </summary>
    public required DateTimeOffset? DeliveryNotAfter { get; set; }

    /// <summary>
    ///     The row version for optimistic concurrency control
    /// </summary>
    public byte[]? RowVersion { get; set; }
}