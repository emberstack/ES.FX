using System.Runtime.CompilerServices;
using System.Text.Json;
using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.NousResearch.HermesAgent.Runs;

/// <summary>
///     Default <see cref="IHermesAgentRunsApi" /> implementation over the shared Hermes Agent
///     <see cref="HttpClient" />.
/// </summary>
internal sealed class HermesAgentRunsApi(HttpClient httpClient, ILogger<HermesAgentRunsApi> logger)
    : HermesAgentResourceApi(httpClient, logger), IHermesAgentRunsApi
{
    /// <inheritdoc />
    public Task<HermesAgentRunCreated> CreateAsync(HermesAgentRunRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<HermesAgentRunCreated>("v1/runs", request, "HermesAgent.Runs.Create", headers,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<HermesAgentRun> GetByIdAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return GetAsync<HermesAgentRun>($"v1/runs/{Uri.EscapeDataString(runId)}", "HermesAgent.Runs.Get",
            cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<HermesAgentRunEvent> StreamEventsAsync(string runId,
        CancellationToken cancellationToken = default)
    {
        // Validate eagerly — the iterator below only runs once the caller starts enumerating.
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return StreamEventsCoreAsync(runId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<HermesAgentRunStopped> StopAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        return PostAsync<HermesAgentRunStopped>($"v1/runs/{Uri.EscapeDataString(runId)}/stop",
            "HermesAgent.Runs.Stop", cancellationToken);
    }

    /// <inheritdoc />
    public Task<HermesAgentRunApprovalResult> ResolveApprovalAsync(string runId,
        HermesAgentRunApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<HermesAgentRunApprovalResult>($"v1/runs/{Uri.EscapeDataString(runId)}/approval", request,
            "HermesAgent.Runs.ResolveApproval", cancellationToken);
    }

    private async IAsyncEnumerable<HermesAgentRunEvent> StreamEventsCoreAsync(string runId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var sseEvent in GetSseAsync($"v1/runs/{Uri.EscapeDataString(runId)}/events",
                           "HermesAgent.Runs.StreamEvents", cancellationToken).ConfigureAwait(false))
            yield return MapEvent(sseEvent);
    }

    /// <summary>
    ///     Maps a raw SSE item onto the typed run-event hierarchy. The run feed is data-only SSE, so the payload's
    ///     <c>event</c> key (not the SSE event type) identifies the event. Unknown names and unparseable payloads
    ///     map to <see cref="HermesAgentRunUnknownEvent" /> — forward compatibility demands the stream never
    ///     fails on data the client does not recognize.
    /// </summary>
    private static HermesAgentRunEvent MapEvent(HermesAgentSseEvent sseEvent)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(sseEvent.Data);
        }
        catch (JsonException)
        {
            return new HermesAgentRunUnknownEvent(sseEvent.EventType, sseEvent.Data);
        }

        using (document)
        {
            var root = document.RootElement;
            var eventName = root.ValueKind == JsonValueKind.Object &&
                            root.TryGetProperty("event", out var eventProperty) &&
                            eventProperty.ValueKind == JsonValueKind.String
                ? eventProperty.GetString()
                : null;

            return eventName switch
            {
                "message.delta" => Deserialize<HermesAgentRunMessageDeltaEvent>(root, sseEvent),
                "tool.started" => Deserialize<HermesAgentRunToolStartedEvent>(root, sseEvent),
                "tool.completed" => Deserialize<HermesAgentRunToolCompletedEvent>(root, sseEvent),
                "reasoning.available" => Deserialize<HermesAgentRunReasoningAvailableEvent>(root, sseEvent),
                "approval.request" => Deserialize<HermesAgentRunApprovalRequestEvent>(root, sseEvent),
                "approval.responded" => Deserialize<HermesAgentRunApprovalRespondedEvent>(root, sseEvent),
                "run.completed" => Deserialize<HermesAgentRunCompletedEvent>(root, sseEvent),
                "run.failed" => Deserialize<HermesAgentRunFailedEvent>(root, sseEvent),
                "run.cancelled" => Deserialize<HermesAgentRunCancelledEvent>(root, sseEvent),
                _ => new HermesAgentRunUnknownEvent(eventName ?? sseEvent.EventType, sseEvent.Data)
            };
        }
    }

    /// <summary>
    ///     Deserializes a known event payload, degrading to <see cref="HermesAgentRunUnknownEvent" /> (raw
    ///     payload preserved) instead of throwing when the payload shape drifts from the modeled fields.
    /// </summary>
    private static HermesAgentRunEvent Deserialize<TEvent>(JsonElement root, HermesAgentSseEvent sseEvent)
        where TEvent : HermesAgentRunEvent
    {
        try
        {
            var mapped = root.Deserialize<TEvent>();
            if (mapped is not null) return mapped;
        }
        catch (JsonException)
        {
            // Fall through to the unknown-event fallback below.
        }

        return new HermesAgentRunUnknownEvent(sseEvent.EventType, sseEvent.Data);
    }
}
