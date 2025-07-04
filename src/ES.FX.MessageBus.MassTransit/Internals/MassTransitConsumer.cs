using ES.FX.MessageBus.Abstractions;
using MassTransit;

namespace ES.FX.MessageBus.MassTransit.Internals;

internal class MassTransitConsumer<TMessage, TMessageHandler>(TMessageHandler handler) : IConsumer<TMessage>
    where TMessage : class
    where TMessageHandler : class, IMessageHandler<TMessage>
{
    public async Task Consume(ConsumeContext<TMessage> context)
    {
        await handler.Handle(context.Message, context.CancellationToken)
            .ConfigureAwait(false);
    }
}