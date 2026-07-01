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
    /// <summary>
    ///     Adds the kind header to the outgoing message. The kind is resolved from the runtime message type and falls
    ///     back to <typeparamref name="T" /> (for example, when publishing an interface contract). If no kind is
    ///     defined, the header is not set.
    /// </summary>
    /// <param name="context">The publish context</param>
    /// <param name="next">The next pipe in the pipeline</param>
    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        var kind = KindAttribute.For(context.Message.GetType()) ?? KindAttribute.For<T>();
        if (kind is not null) context.Headers.Set(MessageKindProvider.Header, kind);
        await next.Send(context);
    }

    /// <summary>
    ///     Creates the probe filter scope
    /// </summary>
    /// <param name="context">The probe context</param>
    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope(typeof(MessageKindPublishFilter<T>).Name);
    }
}