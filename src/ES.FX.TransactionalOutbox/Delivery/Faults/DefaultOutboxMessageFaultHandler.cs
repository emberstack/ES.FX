namespace ES.FX.TransactionalOutbox.Delivery.Faults;

public class DefaultOutboxMessageFaultHandler : IOutboxMessageFaultHandler
{
    private const double InitialSeconds = 10;
    private const double MaxSeconds = 3600;

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