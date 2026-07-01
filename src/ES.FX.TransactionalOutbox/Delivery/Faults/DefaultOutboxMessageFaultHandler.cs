namespace ES.FX.TransactionalOutbox.Delivery.Faults;

/// <summary>
///     Default implementation of <see cref="IOutboxMessageFaultHandler" />. Always schedules the message for
///     redelivery (never discards) using an exponential backoff starting at 10 seconds, doubling on each delivery
///     attempt, and capped at 1 hour.
/// </summary>
public class DefaultOutboxMessageFaultHandler : IOutboxMessageFaultHandler
{
    private const double InitialSeconds = 10;
    private const double MaxSeconds = 3600;

    /// <summary>
    ///     Handles the delivery fault by scheduling the message for redelivery with an exponential backoff delay.
    /// </summary>
    /// <param name="context">The context of the fault</param>
    /// <param name="cancellationToken">Delivery cancellation token</param>
    /// <returns>A <see cref="DeliveryFaultResult" /> that redelivers the message after the computed delay</returns>
    public ValueTask<DeliveryFaultResult> HandleAsync(
        OutboxMessageFaultContext context,
        CancellationToken cancellationToken = default)
    {
        var attempts = Math.Max(1, context.MessageContext.DeliveryAttempts);
        var seconds = Math.Min(
            InitialSeconds * Math.Pow(2, attempts - 1),
            MaxSeconds
        );
        return ValueTask.FromResult(
            DeliveryFaultResult.Redeliver(TimeSpan.FromSeconds(seconds))
        );
    }
}