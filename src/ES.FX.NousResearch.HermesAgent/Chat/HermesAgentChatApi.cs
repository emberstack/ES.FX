using System.Runtime.CompilerServices;
using System.Text.Json;
using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.NousResearch.HermesAgent.Chat;

/// <summary>
///     Default <see cref="IHermesAgentChatApi" /> implementation over the shared Hermes Agent
///     <see cref="HttpClient" />.
/// </summary>
internal sealed class HermesAgentChatApi(HttpClient httpClient, ILogger<HermesAgentChatApi> logger)
    : HermesAgentResourceApi(httpClient, logger), IHermesAgentChatApi
{
    /// <summary>The SSE event name of the Hermes tool-progress extension events.</summary>
    private const string ToolProgressEventType = "hermes.tool.progress";

    /// <summary>The default SSE event name assigned to unnamed <c>data:</c>-only events (the chunks).</summary>
    private const string DefaultEventType = "message";

    /// <summary>
    ///     Options for deserializing SSE payloads — <see cref="JsonSerializerOptions.Web" /> to match the
    ///     defaults <c>ReadFromJsonAsync</c> uses for the non-streaming responses.
    /// </summary>
    private static readonly JsonSerializerOptions ReadJsonOptions = JsonSerializerOptions.Web;

    /// <inheritdoc />
    public Task<HermesAgentChatCompletion> CompleteAsync(HermesAgentChatCompletionRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // The effective (derived/rotated) session id exists ONLY on the X-Hermes-Session-Id response header,
        // so it is stamped onto the returned model here — the body never carries it.
        return PostAsync<HermesAgentChatCompletion>("v1/chat/completions", request with { Stream = false },
            "HermesAgent.Chat.Complete", headers,
            static (completion, responseHeaders) => completion with
            {
                EffectiveSessionId = HermesAgentRequestHeaders.GetEffectiveSessionId(responseHeaders)
            }, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<HermesAgentChatStreamEvent> StreamAsync(HermesAgentChatCompletionRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default)
    {
        // Validate eagerly — the iterator below would defer the throw until enumeration.
        ArgumentNullException.ThrowIfNull(request);
        return StreamCoreAsync(request, headers, cancellationToken);
    }

    private async IAsyncEnumerable<HermesAgentChatStreamEvent> StreamCoreAsync(
        HermesAgentChatCompletionRequest request, HermesAgentRequestHeaders? headers,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The effective session id exists ONLY on the X-Hermes-Session-Id response header. The headers (and
        // the callback) arrive on the FIRST MoveNextAsync, so the enumerator is primed once before deciding
        // whether to yield the synthetic stream-start event ahead of any mapped server event.
        string? effectiveSessionId = null;
        await using var sseEvents = PostSseAsync("v1/chat/completions", request with { Stream = true },
                "HermesAgent.Chat.Stream", headers,
                responseHeaders =>
                    effectiveSessionId = HermesAgentRequestHeaders.GetEffectiveSessionId(responseHeaders),
                cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        var moved = await sseEvents.MoveNextAsync().ConfigureAwait(false);
        if (effectiveSessionId is not null)
            yield return new HermesAgentChatStreamStartEvent(effectiveSessionId);

        while (moved)
        {
            yield return MapStreamEvent(sseEvents.Current);
            moved = await sseEvents.MoveNextAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Maps a raw SSE event onto the typed chat stream hierarchy. Unknown event names and undeserializable
    ///     payloads map to <see cref="HermesAgentChatStreamUnknownEvent" /> — a stream must never fail because
    ///     the server introduced a new event shape.
    /// </summary>
    private static HermesAgentChatStreamEvent MapStreamEvent(HermesAgentSseEvent sseEvent)
    {
        switch (sseEvent.EventType)
        {
            case DefaultEventType:
            {
                var chunk = TryDeserialize<HermesAgentChatCompletionChunk>(sseEvent.Data);
                return chunk is null
                    ? new HermesAgentChatStreamUnknownEvent(sseEvent.EventType, sseEvent.Data)
                    : new HermesAgentChatCompletionChunkEvent(chunk);
            }
            case ToolProgressEventType:
            {
                var progress = TryDeserialize<HermesAgentToolProgress>(sseEvent.Data);
                return progress is null
                    ? new HermesAgentChatStreamUnknownEvent(sseEvent.EventType, sseEvent.Data)
                    : new HermesAgentToolProgressEvent(progress);
            }
            default:
                return new HermesAgentChatStreamUnknownEvent(sseEvent.EventType, sseEvent.Data);
        }
    }

    private static T? TryDeserialize<T>(string data) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(data, ReadJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}