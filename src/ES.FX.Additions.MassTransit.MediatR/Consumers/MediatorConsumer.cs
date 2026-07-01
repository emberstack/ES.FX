using JetBrains.Annotations;
using MassTransit;
using MediatR;

namespace ES.FX.Additions.MassTransit.MediatR.Consumers;

/// <summary>
///     MassTransit consumer that uses <see cref="IMediator" /> to publish/send the consumed message.
/// </summary>
/// <remarks>
///     Dispatch is decided from the <b>runtime message instance</b>: the consumed
///     <see cref="ConsumeContext{TMessage}.Message" /> is pattern-matched, so a single
///     <c>TMessage</c> implementing both <see cref="INotification" /> and <see cref="IRequest" /> is
///     published (notification) in preference to being sent. This differs from
///     <see cref="MediatorBatchConsumer{TMessage}" />, which gates on the static
///     <c>typeof(TMessage)</c> generic parameter instead. The asymmetry is deliberate: a single message
///     has a concrete runtime instance to inspect, whereas a batch has no single instance and must be
///     classified from the element type.
/// </remarks>
/// <typeparam name="TMessage">The type of the message</typeparam>
/// <param name="mediator">Mediator used to publish/send the message</param>
[PublicAPI]
public class MediatorConsumer<TMessage>(IMediator mediator) : IConsumer<TMessage> where TMessage : class
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<TMessage> context)
    {
        switch (context.Message)
        {
            case INotification notification:
                await mediator.Publish(notification, context.CancellationToken);
                break;
            case IRequest request:
                await mediator.Send(request, context.CancellationToken);
                break;
            default:
                throw new InvalidOperationException(
                    $"Message type {typeof(TMessage).Name} is not supported. " +
                    $"It must be either of type {nameof(INotification)} or {nameof(IRequest)}. " +
                    "Requests with a response (IRequest<TResponse>) are not supported");
        }
    }
}