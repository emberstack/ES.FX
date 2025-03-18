using JetBrains.Annotations;
using MassTransit;
using MediatR;

namespace ES.FX.MassTransit.MediatR.Consumers;

/// <summary>
///     MassTransit consumer that uses <see cref="IMediator" /> to publish/send the consumed message
/// </summary>
/// <typeparam name="TMessage">The type of the message</typeparam>
/// <param name="mediator">Mediator used to publish/send the message</param>
[PublicAPI]
public class MediatorConsumer<TMessage>(IMediator mediator) : IConsumer<TMessage> where TMessage : class
{
    public async Task Consume(ConsumeContext<TMessage> context)
    {
        switch (context.Message)
        {
            case INotification notification:
                await mediator.Publish(notification);
                break;
            case IRequest request:
                await mediator.Send(request);
                break;
            default:
                throw new InvalidOperationException(
                    $"Message type {typeof(TMessage).Name} is not supported. " +
                    $"It must be either of type {nameof(INotification)} or {nameof(IRequest)}");
        }
    }
}