using System.Runtime.CompilerServices;
using System.Text.Json;
using ES.FX.NousResearch.HermesAgent.Abstractions;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.NousResearch.HermesAgent.Sessions;

/// <summary>
///     Default <see cref="IHermesAgentSessionsApi" /> implementation over the shared Hermes Agent
///     <see cref="HttpClient" />.
/// </summary>
internal sealed class HermesAgentSessionsApi(HttpClient httpClient, ILogger<HermesAgentSessionsApi> logger)
    : HermesAgentResourceApi(httpClient, logger), IHermesAgentSessionsApi
{
    /// <inheritdoc />
    public Task<HermesAgentSessionsResult> ListAsync(HermesAgentSessionsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = BuildQuery("api/sessions",
            ("limit", QueryInt(query?.Limit)),
            ("offset", QueryInt(query?.Offset)),
            ("source", query?.Source),
            ("include_children", QueryBool(query?.IncludeChildren)));
        return GetAsync<HermesAgentSessionsResult>(requestUri, "HermesAgent.Sessions.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HermesAgentSession> CreateAsync(HermesAgentSessionWrite? session = null,
        CancellationToken cancellationToken = default)
    {
        // QUIRK: the server requires a JSON OBJECT body even for all-default creates (an empty body is a JSON
        // parse error) — a defaulted write model serializes to {} under the omit-null write options.
        var response = await PostAsync<HermesAgentSessionResponse>("api/sessions",
                session ?? new HermesAgentSessionWrite(), "HermesAgent.Sessions.Create", cancellationToken)
            .ConfigureAwait(false);
        return response.Session ?? throw new InvalidOperationException("Hermes Agent returned no created session.");
    }

    /// <inheritdoc />
    public async Task<HermesAgentSession> GetByIdAsync(string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var response = await GetAsync<HermesAgentSessionResponse>(SessionUri(sessionId),
            "HermesAgent.Sessions.Get", cancellationToken).ConfigureAwait(false);
        return response.Session ??
               throw new InvalidOperationException($"Hermes Agent returned no session for '{sessionId}'.");
    }

    /// <inheritdoc />
    public async Task<HermesAgentSession> UpdateAsync(string sessionId, HermesAgentSessionUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(update);

        var response = await PatchAsync<HermesAgentSessionResponse>(SessionUri(sessionId), update,
            "HermesAgent.Sessions.Update", cancellationToken).ConfigureAwait(false);
        return response.Session ??
               throw new InvalidOperationException($"Hermes Agent returned no updated session for '{sessionId}'.");
    }

    /// <inheritdoc />
    public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // The 200 response body ({"object":"hermes.session.deleted","id":...,"deleted":true}) carries nothing
        // actionable beyond success itself, so it is intentionally not surfaced.
        return SendAsync(HttpMethod.Delete, SessionUri(sessionId), null, "HermesAgent.Sessions.Delete",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<HermesAgentSessionMessagesResult> GetMessagesAsync(string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return GetAsync<HermesAgentSessionMessagesResult>($"{SessionUri(sessionId)}/messages",
            "HermesAgent.Sessions.GetMessages", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HermesAgentSession> ForkAsync(string sessionId, HermesAgentSessionForkRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // QUIRK: fork mutates the SOURCE session (end_reason "branched") and requires a JSON object body —
        // a defaulted request serializes to {} for server-generated id/title.
        var response = await PostAsync<HermesAgentSessionResponse>($"{SessionUri(sessionId)}/fork",
                request ?? new HermesAgentSessionForkRequest(), "HermesAgent.Sessions.Fork", cancellationToken)
            .ConfigureAwait(false);
        return response.Session ?? throw new InvalidOperationException("Hermes Agent returned no forked session.");
    }

    /// <inheritdoc />
    public Task<HermesAgentSessionChatCompletion> ChatAsync(string sessionId, HermesAgentSessionChatRequest request,
        HermesAgentRequestHeaders? headers = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(request);

        return PostAsync<HermesAgentSessionChatCompletion>($"{SessionUri(sessionId)}/chat", request,
            "HermesAgent.Sessions.Chat", headers, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<HermesAgentSessionChatEvent> StreamChatAsync(string sessionId,
        HermesAgentSessionChatRequest request, HermesAgentRequestHeaders? headers = null,
        CancellationToken cancellationToken = default)
    {
        // Validate eagerly — the iterator below would defer the throw until enumeration.
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(request);
        return StreamChatCoreAsync(sessionId, request, headers, cancellationToken);
    }

    private async IAsyncEnumerable<HermesAgentSessionChatEvent> StreamChatCoreAsync(string sessionId,
        HermesAgentSessionChatRequest request, HermesAgentRequestHeaders? headers,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var sseEvent in PostSseAsync($"{SessionUri(sessionId)}/chat/stream", request,
                           "HermesAgent.Sessions.StreamChat", headers, cancellationToken).ConfigureAwait(false))
            yield return MapEvent(sseEvent);
    }

    /// <summary>
    ///     Maps a raw SSE event onto the typed session chat event hierarchy. Unknown event types AND payloads
    ///     that fail to parse map to <see cref="HermesAgentSessionChatUnknownEvent" /> — this method never
    ///     throws, so a new server event type cannot break an in-flight stream.
    /// </summary>
    private static HermesAgentSessionChatEvent MapEvent(HermesAgentSseEvent sseEvent)
    {
        try
        {
            HermesAgentSessionChatEvent? mapped = sseEvent.EventType switch
            {
                "run.started" => JsonSerializer.Deserialize<HermesAgentSessionChatRunStartedEvent>(sseEvent.Data),
                "message.started" =>
                    JsonSerializer.Deserialize<HermesAgentSessionChatMessageStartedEvent>(sseEvent.Data),
                "assistant.delta" =>
                    JsonSerializer.Deserialize<HermesAgentSessionChatAssistantDeltaEvent>(sseEvent.Data),
                "tool.progress" =>
                    JsonSerializer.Deserialize<HermesAgentSessionChatToolProgressEvent>(sseEvent.Data),
                "tool.started" => JsonSerializer.Deserialize<HermesAgentSessionChatToolStartedEvent>(sseEvent.Data),
                "tool.completed" =>
                    JsonSerializer.Deserialize<HermesAgentSessionChatToolCompletedEvent>(sseEvent.Data),
                "tool.failed" => JsonSerializer.Deserialize<HermesAgentSessionChatToolFailedEvent>(sseEvent.Data),
                "assistant.completed" =>
                    JsonSerializer.Deserialize<HermesAgentSessionChatAssistantCompletedEvent>(sseEvent.Data),
                "run.completed" =>
                    JsonSerializer.Deserialize<HermesAgentSessionChatRunCompletedEvent>(sseEvent.Data),
                "error" => JsonSerializer.Deserialize<HermesAgentSessionChatErrorEvent>(sseEvent.Data),
                "done" => JsonSerializer.Deserialize<HermesAgentSessionChatDoneEvent>(sseEvent.Data),
                _ => null
            };
            return mapped ?? new HermesAgentSessionChatUnknownEvent(sseEvent.EventType, sseEvent.Data);
        }
        catch (JsonException)
        {
            return new HermesAgentSessionChatUnknownEvent(sseEvent.EventType, sseEvent.Data);
        }
    }

    /// <summary>
    ///     Builds the resource path for a session id. The id is escaped because ids from other channels (and
    ///     fork, which skips the create-time path-unsafety checks) are not guaranteed to be URL-safe.
    /// </summary>
    private static string SessionUri(string sessionId) => $"api/sessions/{Uri.EscapeDataString(sessionId)}";
}
