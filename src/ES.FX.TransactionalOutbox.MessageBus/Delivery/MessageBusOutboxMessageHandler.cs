using ES.FX.MessageBus.Abstractions;
using ES.FX.TransactionalOutbox.Delivery;
using JetBrains.Annotations;

namespace ES.FX.TransactionalOutbox.MessageBus.Delivery;

/// <summary>
///     Message handler that uses a <see cref="IMessageBus" /> to publish outbox messages
/// </summary>
[PublicAPI]
public class MessageBusOutboxMessageHandler(IMessageBus messageBus, IMessageBusControl messageBusControl)
    : IOutboxMessageHandler
{
    public async ValueTask Handle(OutboxMessageContext context,
        CancellationToken cancellationToken = default)
    {
        await messageBus.Publish(context.Message, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> IsReady(CancellationToken cancellation = default) =>
        await messageBusControl.IsReadyAsync(cancellation).ConfigureAwait(false);
}