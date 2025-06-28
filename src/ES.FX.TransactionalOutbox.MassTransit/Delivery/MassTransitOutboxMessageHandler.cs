using ES.FX.TransactionalOutbox.Delivery;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.TransactionalOutbox.MassTransit.Delivery;

/// <summary>
///     Message handler that uses a <see cref="IPublishEndpoint" /> to publish outbox messages
/// </summary>
/// <param name="busControl">The <see cref="IBusControl" /> used to check if the handler can publish messages</param>
/// <param name="publishEndpoint">The endpoint used to publish messages</param>
[PublicAPI]
public class MassTransitOutboxMessageHandler(IBusControl busControl, IPublishEndpoint publishEndpoint)
    : IOutboxMessageHandler
{
    public async ValueTask HandleAsync(OutboxMessageContext context,
        CancellationToken cancellationToken = default)
    {
        await publishEndpoint.Publish(context.Message, cancellationToken);
    }

    public async ValueTask<bool> IsReadyAsync(CancellationToken cancellation = default) =>
        await busControl.WaitForHealthStatus(BusHealthStatus.Healthy, cancellation).ConfigureAwait(false) ==
        BusHealthStatus.Healthy;
}