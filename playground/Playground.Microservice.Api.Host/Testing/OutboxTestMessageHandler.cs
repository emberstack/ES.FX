using ES.FX.MessageBus.Abstractions;

namespace Playground.Microservice.Api.Host.Testing;

public class OutboxTestMessageHandler :
    IMessageHandler<OutboxTestMessage>,
    IMessageHandler<OutboxTestMessage2>
{
    public async Task Handle(OutboxTestMessage message, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }

    public async Task Handle(OutboxTestMessage2 message, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }
}


public class OutboxTestMessageHandler2 :
    IMessageHandler<OutboxTestMessage>
{

    public async Task Handle(OutboxTestMessage message, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }
}








//public sealed class MassTransitMessageConsumer<TMessage, TMessageHandler>(TMessageHandler handler)
//    : IConsumer<TMessage> where TMessage : class, IMessage where TMessageHandler : class, IMessageHandler<TMessage>
//{
//    public async Task Consume(ConsumeContext<TMessage> context)
//    {
//        await handler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
//    }
//}


//public sealed class MassTransitMessageConsumer2<TMessage,THandler>:IConsumer<OutboxTestMessage>
//{
//    public async Task Consume(ConsumeContext<OutboxTestMessage> context)
//    {
//        await Task.CompletedTask;
//    }
//}