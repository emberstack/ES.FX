using ES.FX.TransactionalOutbox.Delivery;

namespace ES.FX.TransactionalOutbox.Interceptors;

/// <summary>
///     Context for the outbox message interceptor.
/// </summary>
public record OutboxMessageInterceptorContext
{
    /// <summary>
    ///     The date and time at which the message was added to the outbox.
    /// </summary>
    public required DateTimeOffset AddedAt { get; init; }

    /// <summary>
    ///     The headers associated with the outbox message.
    /// </summary>
    public required IDictionary<string, string> Headers { get; init; }

    /// <summary>
    ///     The ID of the activity associated with the outbox message, if any.
    /// </summary>
    public string? ActivityId { get; init; }

    /// <summary>
    ///     The delivery options for the outbox message, which can include settings such as maximum delivery attempts, delay
    ///     between attempts, and timing constraints.
    /// </summary>
    public OutboxMessageDeliveryOptions? DeliveryOptions { get; init; }

    /// <summary>
    ///     The payload of the outbox message, which is the actual data being sent.
    /// </summary>
    public required object Payload { get; init; }

    /// <summary>
    ///     The type of the payload being sent in the outbox message. This is used to ensure that the correct serialization and
    ///     deserialization processes are applied.
    /// </summary>
    public required Type PayloadType { get; init; }
}