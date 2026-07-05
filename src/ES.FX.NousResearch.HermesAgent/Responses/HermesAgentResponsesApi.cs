using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.NousResearch.HermesAgent.Responses;

/// <summary>
///     Default <see cref="IHermesAgentResponsesApi" /> implementation over the shared Hermes Agent
///     <see cref="HttpClient" />.
/// </summary>
internal sealed class HermesAgentResponsesApi(HttpClient httpClient, ILogger<HermesAgentResponsesApi> logger)
    : HermesAgentResourceApi(httpClient, logger), IHermesAgentResponsesApi
{
    // SSE event names of the streaming Responses API (each payload also repeats the name as its "type" field).
    private const string CreatedEventName = "response.created";
    private const string OutputItemAddedEventName = "response.output_item.added";
    private const string OutputTextDeltaEventName = "response.output_text.delta";
    private const string OutputTextDoneEventName = "response.output_text.done";
    private const string OutputItemDoneEventName = "response.output_item.done";
    private const string CompletedEventName = "response.completed";
    private const string FailedEventName = "response.failed";

    /// <inheritdoc />
    public Task<HermesAgentResponse> CreateAsync(HermesAgentResponseRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // The effective (derived/rotated) session id exists ONLY on the X-Hermes-Session-Id response header,
        // so it is stamped onto the returned envelope here — the body never carries it.
        return PostAsync<HermesAgentResponse>("v1/responses", CreatePayload(request, stream: false),
            "HermesAgent.Responses.Create", headers,
            static (response, responseHeaders) => response with
            {
                EffectiveSessionId = HermesAgentRequestHeaders.GetEffectiveSessionId(responseHeaders)
            }, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<HermesAgentResponseStreamEvent> StreamAsync(HermesAgentResponseRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return StreamCoreAsync(request, headers, cancellationToken);
    }

    /// <inheritdoc />
    public Task<HermesAgentResponse> GetByIdAsync(string responseId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseId);
        return GetAsync<HermesAgentResponse>($"v1/responses/{Uri.EscapeDataString(responseId)}",
            "HermesAgent.Responses.Get", cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string responseId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseId);
        // The 200 acknowledgement body ({"id", "object", "deleted": true}) carries nothing the caller needs.
        return SendAsync(HttpMethod.Delete, $"v1/responses/{Uri.EscapeDataString(responseId)}", null,
            "HermesAgent.Responses.Delete", cancellationToken);
    }

    private async IAsyncEnumerable<HermesAgentResponseStreamEvent> StreamCoreAsync(
        HermesAgentResponseRequest request, HermesAgentRequestHeaders? headers,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The effective session id exists ONLY on the X-Hermes-Session-Id response header. The headers (and
        // the callback) arrive on the FIRST MoveNextAsync, so the enumerator is primed once before deciding
        // whether to yield the synthetic stream-start event ahead of any mapped server event.
        string? effectiveSessionId = null;
        await using var sseEvents = PostSseAsync("v1/responses", CreatePayload(request, stream: true),
                "HermesAgent.Responses.Stream", headers,
                responseHeaders =>
                    effectiveSessionId = HermesAgentRequestHeaders.GetEffectiveSessionId(responseHeaders),
                cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        var moved = await sseEvents.MoveNextAsync().ConfigureAwait(false);
        if (effectiveSessionId is not null)
            yield return new HermesAgentResponseStreamStartEvent(effectiveSessionId);

        while (moved)
        {
            yield return MapEvent(sseEvents.Current);
            moved = await sseEvents.MoveNextAsync().ConfigureAwait(false);
        }
    }

    private static JsonObject CreatePayload(HermesAgentResponseRequest request, bool stream)
    {
        // The streaming mode is selected by the method used (CreateAsync vs StreamAsync), never by the caller's
        // request object (which deliberately has no stream property), so the flag is stamped onto the
        // serialized body here. WriteJsonOptions already dropped the unset (null) fields.
        var payload = JsonSerializer.SerializeToNode(request, WriteJsonOptions)!.AsObject();
        payload["stream"] = stream;
        return payload;
    }

    private static HermesAgentResponseStreamEvent MapEvent(HermesAgentSseEvent sseEvent) =>
        sseEvent.EventType switch
        {
            CreatedEventName => DeserializeEvent<HermesAgentResponseCreatedEvent>(sseEvent),
            OutputItemAddedEventName => DeserializeEvent<HermesAgentResponseOutputItemAddedEvent>(sseEvent),
            OutputTextDeltaEventName => DeserializeEvent<HermesAgentResponseOutputTextDeltaEvent>(sseEvent),
            OutputTextDoneEventName => DeserializeEvent<HermesAgentResponseOutputTextDoneEvent>(sseEvent),
            OutputItemDoneEventName => DeserializeEvent<HermesAgentResponseOutputItemDoneEvent>(sseEvent),
            CompletedEventName => DeserializeEvent<HermesAgentResponseCompletedEvent>(sseEvent),
            FailedEventName => DeserializeEvent<HermesAgentResponseFailedEvent>(sseEvent),
            _ => new HermesAgentResponseUnknownEvent(sseEvent.EventType, sseEvent.Data)
        };

    private static HermesAgentResponseStreamEvent DeserializeEvent<TEvent>(HermesAgentSseEvent sseEvent)
        where TEvent : HermesAgentResponseStreamEvent
    {
        try
        {
            HermesAgentResponseStreamEvent? parsed = JsonSerializer.Deserialize<TEvent>(sseEvent.Data);
            return parsed ?? new HermesAgentResponseUnknownEvent(sseEvent.EventType, sseEvent.Data);
        }
        catch (JsonException)
        {
            // Forward compatible: a known event whose payload no longer parses degrades to the raw fallback
            // instead of failing the whole stream.
            return new HermesAgentResponseUnknownEvent(sseEvent.EventType, sseEvent.Data);
        }
    }
}
