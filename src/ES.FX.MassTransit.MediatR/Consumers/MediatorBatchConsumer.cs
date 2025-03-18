using ES.FX.MediatR.Abstractions.Contracts;
using JetBrains.Annotations;
using MassTransit;
using MediatR;

namespace ES.FX.MassTransit.MediatR.Consumers;

/// <summary>
///     MassTransit consumer that uses <see cref="IMediator" /> to publish/send the consumed message batches
/// </summary>
/// <typeparam name="TMessage">The type of the message</typeparam>
/// <param name="mediator">Mediator used to publish/send the batch of messages</param>
[PublicAPI]
public class MediatorBatchConsumer<TMessage>(IMediator mediator) : IConsumer<Batch<TMessage>> where TMessage : class
{
    public async Task Consume(ConsumeContext<Batch<TMessage>> context)
    {
        if (context.Message.Length == 0) return;

        var messages = context.Message.Select(s => s.Message).ToArray();

        if (typeof(TMessage).IsAssignableTo(typeof(INotification)))
            await mediator.Publish(new BatchNotification<TMessage>(messages));
        else if (typeof(TMessage).IsAssignableTo(typeof(IRequest)))
            await mediator.Publish(new BatchRequest<TMessage>(messages));
        else
            throw new InvalidOperationException(
                $"Message type {typeof(TMessage).Name} is not supported. " +
                $"It must be either of type {nameof(INotification)} or {nameof(IRequest)}");
    }
}