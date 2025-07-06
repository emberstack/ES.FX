using MassTransit;

namespace ES.FX.MessageBus.MassTransit.Internals;

internal class MassTransitMessageContext<TMessage>(TMessage message, IPublishEndpoint publishEndpoint) : IMessageContext<TMessage> where TMessage : class
{
    object IMessageContext.Message => Message;

    public TMessage Message => message;

    public async Task Publish<TMessageToPublish>(TMessageToPublish message, CancellationToken cancellationToken = default) where TMessageToPublish : class
    {
        await publishEndpoint.Publish(message, cancellationToken).ConfigureAwait(false);
    }
}
