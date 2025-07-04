using ES.FX.MessageBus.Abstractions;
using MassTransit;

namespace ES.FX.MessageBus.MassTransit.Internals;

internal class MassTransitMessageBus(IPublishEndpoint publishEndpoint): IMessageBus
{
    public async Task Publish<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class => await publishEndpoint.Publish((object)message, cancellationToken).ConfigureAwait(false);
}