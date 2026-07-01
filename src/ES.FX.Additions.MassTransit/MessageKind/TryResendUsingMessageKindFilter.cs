using System.Collections.Concurrent;
using JetBrains.Annotations;
using MassTransit;
using MassTransit.Serialization;

namespace ES.FX.Additions.MassTransit.MessageKind;

/// <summary>
///     Filter that attempts to fix the message type and resend the message.
///     <para>
///         This filter is intended to be attached to the dead-letter/skipped pipe (for example via
///         <c>ConfigureDeadLetter(pipe =&gt; pipe.UseFilter(new TryResendUsingMessageKindFilter()))</c>). Do not attach
///         it to a regular receive pipe: the kind header is stamped on every published message, so every
///         kind-annotated message would be resent instead of consumed.
///     </para>
/// </summary>
[PublicAPI]
public class TryResendUsingMessageKindFilter : IFilter<ReceiveContext>
{
    // Keyed by media type (case-insensitive), not the ContentType instance: ContentType does not override
    // Equals/GetHashCode (so it keys by reference identity), and the full content-type string carries
    // charset/boundary parameters — either would let this static cache grow without bound as content-type
    // instances/parameters vary. The set of media types is small and finite, which keeps the cache bounded.
    private static readonly ConcurrentDictionary<string, SystemTextJsonMessageSerializer> Serializers =
        new(StringComparer.OrdinalIgnoreCase);

    // ReceiveContext.ContentType is null when the transport message carries no Content-Type header; the
    // serializer then falls back to its default JSON content type, matching new SystemTextJsonMessageSerializer(null).
    private static readonly SystemTextJsonMessageSerializer DefaultSerializer = new();

    Task IFilter<ReceiveContext>.Send(ReceiveContext context, IPipe<ReceiveContext> next)
    {
        var messageType = MessageKindProvider.GetType(context);
        if (messageType is null) return next.Send(context);

        var contentType = context.ContentType;
        var serializer = contentType is null
            ? DefaultSerializer
            : Serializers.GetOrAdd(contentType.MediaType,
                static (_, ct) => new SystemTextJsonMessageSerializer(ct), contentType);
        var consumeContext = serializer.Deserialize(context);
        return consumeContext.SerializerContext.TryGetMessage(messageType, out var message)
            ? consumeContext.Send(context.InputAddress, message, Pipe.Execute<SendContext>(sendContext =>
            {
                sendContext.RequestId = consumeContext.RequestId;
                sendContext.ResponseAddress = consumeContext.ResponseAddress;
                sendContext.FaultAddress = consumeContext.FaultAddress;
            }))
            : next.Send(context);
    }

    void IProbeSite.Probe(ProbeContext context)
    {
        context.CreateFilterScope(nameof(TryResendUsingMessageKindFilter));
    }
}