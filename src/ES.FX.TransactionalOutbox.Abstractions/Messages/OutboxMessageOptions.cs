namespace ES.FX.TransactionalOutbox.Abstractions.Messages;

/// <summary>
///     Options for delivering the outbox message
/// </summary>
public class OutboxMessageOptions
{
    /// <summary>
    ///     Maximum number of attempts to deliver the message before it is marked as faulted
    /// </summary>
    public int? MaxAttempts { get; set; }

    /// <summary>
    ///     The time at which the message should be delivered. If this is null, the message will be delivered immediately
    /// </summary>
    public DateTimeOffset? NotBefore { get; set; }

    /// <summary>
    ///     The time at which the message should not be delivered after. If the message is not delivered by this time, it will
    ///     be discarded
    /// </summary>
    public DateTimeOffset? NotAfter { get; set; }

    /// <summary>
    ///     The minimum delay between delivery attempts in seconds
    /// </summary>
    public int DelayBetweenAttempts { get; set; }

    /// <summary>
    ///     If true, the delay between delivery attempts will be exponential
    /// </summary>
    public bool DelayBetweenAttemptsIsExponential { get; set; }
}