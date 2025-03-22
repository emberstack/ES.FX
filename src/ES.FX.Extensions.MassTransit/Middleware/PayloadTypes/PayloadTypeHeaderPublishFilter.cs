using ES.FX.ComponentModel.DataAnnotations;
using ES.FX.Extensions.MassTransit.Serialization;
using JetBrains.Annotations;
using MassTransit;

namespace ES.FX.Extensions.MassTransit.Middleware.PayloadTypes;

/// <summary>
///     Filter that adds the message type to the headers of the message.
/// </summary>
[PublicAPI]
public class PayloadTypeHeaderPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        context.Headers.Set(
            MassTransitPayloadTypeProvider.Header,
            PayloadTypeAttribute.PayloadTypeFor(context.Message.GetType()));
        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope(typeof(PayloadTypeHeaderPublishFilter<>)
            .MakeGenericType(typeof(T)).Name);
    }
}