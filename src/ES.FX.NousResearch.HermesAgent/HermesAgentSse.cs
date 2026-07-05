using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace ES.FX.NousResearch.HermesAgent;

/// <summary>
///     A raw server-sent event read from a Hermes Agent SSE stream. <see cref="EventType" /> is
///     <c>message</c> for unnamed <c>data:</c>-only events (the SSE default) and the <c>event:</c> value for
///     named events (e.g. <c>hermes.tool.progress</c>, <c>response.completed</c>). Area implementations map
///     these onto their typed event hierarchies.
/// </summary>
internal sealed record HermesAgentSseEvent(string EventType, string Data);

/// <summary>
///     Enumerates a Hermes Agent SSE response body into <see cref="HermesAgentSseEvent" /> items using the
///     in-box <see cref="SseParser" />. Keepalive comments (<c>: keepalive</c>, <c>: stream closed</c>) are not
///     surfaced by the parser; the <c>data: [DONE]</c> terminator (used by the chat-completions stream) ends the
///     enumeration without being yielded.
/// </summary>
internal static class HermesAgentSse
{
    /// <summary>The stream-terminator sentinel payload used by OpenAI-compatible SSE streams.</summary>
    public const string DoneSentinel = "[DONE]";

    /// <summary>
    ///     Enumerates the server-sent events of <paramref name="stream" />, stopping at end-of-stream or at the
    ///     first <c>[DONE]</c> data payload (whichever comes first).
    /// </summary>
    public static async IAsyncEnumerable<HermesAgentSseEvent> EnumerateAsync(Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in SseParser.Create(stream).EnumerateAsync(cancellationToken).ConfigureAwait(false))
        {
            // Streams that use the OpenAI terminator (chat completions) send it as a plain data payload;
            // streams that end with a terminal named event simply close — both paths end the enumeration here.
            if (string.Equals(item.Data, DoneSentinel, StringComparison.Ordinal))
                yield break;

            yield return new HermesAgentSseEvent(item.EventType, item.Data);
        }
    }
}
