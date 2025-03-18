using ES.FX.MassTransit.Serialization;
using JetBrains.Annotations;
using MassTransit;
using MassTransit.Serialization;

namespace ES.FX.MassTransit.Middleware.PayloadTypes;

/// <summary>
///     Filter that attempts to fix the message type and resend the message
/// </summary>
[PublicAPI]
public class DeadLetterResolvePayloadTypeFilter : IFilter<ReceiveContext>
{
    Task IFilter<ReceiveContext>.Send(ReceiveContext context, IPipe<ReceiveContext> next)
    {
        var messageType = MassTransitPayloadTypeProvider.GetType(context);
        if (messageType is null) return next.Send(context);

        var consumeContext = new SystemTextJsonMessageSerializer(context.ContentType).Deserialize(context);
        return consumeContext.SerializerContext.TryGetMessage(messageType, out var message)
            ? consumeContext.Send(context.InputAddress, message)
            : next.Send(context);
    }

    void IProbeSite.Probe(ProbeContext context)
    {
        context.CreateFilterScope(nameof(DeadLetterResolvePayloadTypeFilter));
    }
}