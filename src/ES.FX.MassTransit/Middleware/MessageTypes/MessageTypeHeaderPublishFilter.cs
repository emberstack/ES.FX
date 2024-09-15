using ES.FX.Contracts.Messaging;
using ES.FX.MassTransit.Serialization;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.MassTransit.Middleware.MessageTypes;

/// <summary>
///     Filter that adds the message type to the headers of the message.
/// </summary>
[PublicAPI]
public class MessageTypeHeaderPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        context.Headers.Set(
            MassTransitMessageTypeProvider.Header,
            MessageTypeAttribute.MessageTypeFor(context.Message.GetType()));
        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope(typeof(MessageTypeHeaderPublishFilter<>)
            .MakeGenericType(typeof(T)).Name);
    }
}