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
    /// <inheritdoc />
    public async ValueTask HandleAsync(OutboxMessageContext context,
        CancellationToken cancellationToken = default)
    {
        await publishEndpoint.Publish(context.Message, context.MessageType, ctx =>
        {
            foreach (var (key, value) in context.Headers) ctx.Headers.Set(key, value);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Returns <see langword="true" /> when the bus health status is <see cref="BusHealthStatus.Healthy" />.
    ///     The check is non-blocking; the delivery service polls readiness on its own interval.
    /// </remarks>
    public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(busControl.CheckHealth().Status == BusHealthStatus.Healthy);
}