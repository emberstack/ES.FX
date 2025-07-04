using ES.FX.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.Additions.MassTransit.MessageKind;

/// <summary>
///     Filter that adds the Kind to the headers of the message.
/// </summary>
[PublicAPI]
public class MessageKindPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        context.Headers.Set(
            MessageKindProvider.Header,
            KindAttribute.For(context.Message.GetType()));
        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope(typeof(MessageKindPublishFilter<>)
            .MakeGenericType(typeof(T)).Name);
    }
}