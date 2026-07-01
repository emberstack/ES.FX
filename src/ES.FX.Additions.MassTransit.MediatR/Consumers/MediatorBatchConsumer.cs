using ES.FX.Additions.MediatR.Contracts.Batches;
using JetBrains.Annotations;
using MassTransit;
using MediatR;

namespace ES.FX.Additions.MassTransit.MediatR.Consumers;

/// <summary>
///     MassTransit consumer that uses <see cref="IMediator" /> to publish/send the consumed message batches.
/// </summary>
/// <remarks>
///     Dispatch is decided from the <b>static <c>typeof(TMessage)</c> generic parameter</b> (via
///     <see cref="Type.IsAssignableTo" />), not from any individual message instance in the batch, so a
///     <c>TMessage</c> implementing both <see cref="INotification" /> and <see cref="IRequest" /> is
///     published (notification) in preference to being sent. This differs from
///     <see cref="MediatorConsumer{TMessage}" />, which decides from the runtime message instance. The
///     asymmetry is deliberate: a batch has no single message instance to inspect and must be classified
///     from the element type, whereas a single message can be matched on its concrete runtime instance.
/// </remarks>
/// <typeparam name="TMessage">The type of the message</typeparam>
/// <param name="mediator">Mediator used to publish/send the batch of messages</param>
[PublicAPI]
public class MediatorBatchConsumer<TMessage>(IMediator mediator) : IConsumer<Batch<TMessage>> where TMessage : class
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<Batch<TMessage>> context)
    {
        if (context.Message.Length == 0) return;

        var messages = new TMessage[context.Message.Length];
        for (var i = 0; i < messages.Length; i++)
            messages[i] = context.Message[i].Message;

        if (typeof(TMessage).IsAssignableTo(typeof(INotification)))
            await mediator.Publish(new BatchNotification<TMessage>
            {
                Items = messages
            }, context.CancellationToken);
        else if (typeof(TMessage).IsAssignableTo(typeof(IRequest)))
            await mediator.Send(new BatchRequest<TMessage>
            {
                Items = messages
            }, context.CancellationToken);
        else
            throw new InvalidOperationException(
                $"Message type {typeof(TMessage).Name} is not supported. " +
                $"It must be either of type {nameof(INotification)} or {nameof(IRequest)}. " +
                "Requests with a response (IRequest<TResponse>) are not supported");
    }
}