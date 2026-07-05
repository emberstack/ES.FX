using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.NousResearch.HermesAgent;

/// <summary>
///     Base class for resource-area API implementations. Centralizes the request-and-deserialize flow with a
///     Hermes Agent domain <see cref="Activity" /> (status + exception), non-success handling
///     (<see cref="HermesAgentApiException" /> via <see cref="HermesAgentResponseGuard" />), logging, and the
///     SSE streaming flow for the server's <c>text/event-stream</c> endpoints.
/// </summary>
internal abstract class HermesAgentResourceApi(HttpClient httpClient, ILogger logger)
{
    /// <summary>
    ///     Serializer options for request bodies: <c>null</c> properties are omitted, so a partial update sends
    ///     only the fields the caller actually set (models carry explicit <c>JsonPropertyName</c> attributes).
    /// </summary>
    protected static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///     The shared Hermes Agent <see cref="HttpClient" /> (base address + auth handler), for subclasses that
    ///     need raw access beyond the helpers.
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    protected Task<TResponse> GetAsync<TResponse>(string requestUri, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Get, requestUri, null, operation, null, cancellationToken);

    protected Task<TResponse> PostAsync<TResponse>(string requestUri, object payload, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Post, requestUri,
            JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions), operation, null,
            cancellationToken);

    /// <summary>Sends a <c>POST</c> with optional per-call headers (session continuity / idempotency).</summary>
    protected Task<TResponse> PostAsync<TResponse>(string requestUri, object payload, string operation,
        HermesAgentRequestHeaders? headers, CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Post, requestUri,
            JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions), operation, headers, null,
            cancellationToken);

    /// <summary>
    ///     Sends a <c>POST</c> with optional per-call headers and a post-deserialize
    ///     <paramref name="enrichFromResponseHeaders" /> hook that lets the area API stamp response-header-only
    ///     data (e.g. the effective <c>X-Hermes-Session-Id</c>) onto the deserialized payload.
    /// </summary>
    protected Task<TResponse> PostAsync<TResponse>(string requestUri, object payload, string operation,
        HermesAgentRequestHeaders? headers,
        Func<TResponse, HttpResponseHeaders, TResponse> enrichFromResponseHeaders,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, requestUri,
            JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions), operation, headers,
            enrichFromResponseHeaders, cancellationToken);

    /// <summary>Sends a body-less <c>POST</c> (e.g. pause/resume/stop style operations).</summary>
    protected Task<TResponse> PostAsync<TResponse>(string requestUri, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Post, requestUri, null, operation, null, cancellationToken);

    protected Task<TResponse> PatchAsync<TResponse>(string requestUri, object payload, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Patch, requestUri,
            JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions), operation, null,
            cancellationToken);

    protected Task<TResponse> DeleteAsync<TResponse>(string requestUri, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Delete, requestUri, null, operation, null, cancellationToken);

    /// <summary>Sends a request whose success response carries no payload the caller needs.</summary>
    protected async Task SendAsync(HttpMethod method, string requestUri, HttpContent? content, string operation,
        CancellationToken cancellationToken)
    {
        using var activity = HermesAgentClientInstrumentation.ActivitySource
            .StartActivity(operation, ActivityKind.Client);
        activity?.SetTag("hermesagent.operation", operation);

        try
        {
            using var request = new HttpRequestMessage(method, requestUri) { Content = content };
            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await HermesAgentResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            activity?.SetStatus(ActivityStatusCode.Ok);
            HermesAgentClientLog.OperationSucceeded(logger, operation);
        }
        catch (Exception exception)
        {
            RecordFailure(activity, exception, operation);
            throw;
        }
    }

    /// <summary>Sends a request with optional per-call headers and deserializes the JSON response body.</summary>
    protected Task<TResponse> SendAsync<TResponse>(HttpMethod method, string requestUri, HttpContent? content,
        string operation, HermesAgentRequestHeaders? headers, CancellationToken cancellationToken) =>
        SendAsync<TResponse>(method, requestUri, content, operation, headers, null, cancellationToken);

    /// <summary>
    ///     Sends a request and deserializes the JSON response body. When
    ///     <paramref name="enrichFromResponseHeaders" /> is provided it runs after deserialization with the
    ///     response headers, so header-only data (e.g. the effective <c>X-Hermes-Session-Id</c>) can be stamped
    ///     onto the returned model before the response is disposed.
    /// </summary>
    protected async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string requestUri, HttpContent? content,
        string operation, HermesAgentRequestHeaders? headers,
        Func<TResponse, HttpResponseHeaders, TResponse>? enrichFromResponseHeaders,
        CancellationToken cancellationToken)
    {
        using var activity = HermesAgentClientInstrumentation.ActivitySource
            .StartActivity(operation, ActivityKind.Client);
        activity?.SetTag("hermesagent.operation", operation);

        try
        {
            using var request = new HttpRequestMessage(method, requestUri) { Content = content };
            headers?.Apply(request);
            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await HermesAgentResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            var payload = await response.Content
                .ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false);
            if (payload is null)
                throw new InvalidOperationException($"Hermes Agent returned an empty response for '{operation}'.");

            if (enrichFromResponseHeaders is not null)
                payload = enrichFromResponseHeaders(payload, response.Headers);

            activity?.SetStatus(ActivityStatusCode.Ok);
            HermesAgentClientLog.OperationSucceeded(logger, operation);
            return payload;
        }
        catch (Exception exception)
        {
            RecordFailure(activity, exception, operation);
            throw;
        }
    }

    /// <summary>Streams a <c>GET</c> SSE endpoint (e.g. <c>GET v1/runs/{id}/events</c>).</summary>
    protected IAsyncEnumerable<HermesAgentSseEvent> GetSseAsync(string requestUri, string operation,
        CancellationToken cancellationToken) =>
        SendSseAsync(HttpMethod.Get, requestUri, null, operation, null, null, cancellationToken);

    /// <summary>Streams a <c>POST</c> SSE endpoint (chat/responses/session-chat with <c>stream: true</c>).</summary>
    protected IAsyncEnumerable<HermesAgentSseEvent> PostSseAsync(string requestUri, object payload, string operation,
        HermesAgentRequestHeaders? headers, CancellationToken cancellationToken) =>
        SendSseAsync(HttpMethod.Post, requestUri,
            JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions), operation, headers, null,
            cancellationToken);

    /// <summary>
    ///     Streams a <c>POST</c> SSE endpoint with an <paramref name="onResponseHeaders" /> callback invoked once
    ///     the response headers arrive (before any event is yielded), so the area API can surface
    ///     response-header-only data (e.g. the effective <c>X-Hermes-Session-Id</c>) to its stream consumers.
    /// </summary>
    protected IAsyncEnumerable<HermesAgentSseEvent> PostSseAsync(string requestUri, object payload, string operation,
        HermesAgentRequestHeaders? headers, Action<HttpResponseHeaders> onResponseHeaders,
        CancellationToken cancellationToken) =>
        SendSseAsync(HttpMethod.Post, requestUri,
            JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions), operation, headers,
            onResponseHeaders, cancellationToken);

    /// <summary>
    ///     Sends a request and enumerates its <c>text/event-stream</c> response as raw
    ///     <see cref="HermesAgentSseEvent" /> items (keepalive comments skipped, <c>[DONE]</c> honored as the
    ///     terminator). The <see cref="Activity" /> spans the entire stream consumption; the request is lazy —
    ///     nothing is sent until the caller starts enumerating. <paramref name="onResponseHeaders" /> (optional)
    ///     is invoked with the response headers after the success check, before the first event is yielded.
    /// </summary>
    protected async IAsyncEnumerable<HermesAgentSseEvent> SendSseAsync(HttpMethod method, string requestUri,
        HttpContent? content, string operation, HermesAgentRequestHeaders? headers,
        Action<HttpResponseHeaders>? onResponseHeaders,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var activity = HermesAgentClientInstrumentation.ActivitySource
            .StartActivity(operation, ActivityKind.Client);
        activity?.SetTag("hermesagent.operation", operation);

        using var request = new HttpRequestMessage(method, requestUri) { Content = content };
        headers?.Apply(request);

        HttpResponseMessage response;
        try
        {
            // ResponseHeadersRead: the SSE body must stream — buffering would block until the server closes it.
            response = await HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordFailure(activity, exception, operation);
            throw;
        }

        using (response)
        {
            Stream stream;
            try
            {
                await HermesAgentResponseGuard.EnsureSuccessAsync(response, cancellationToken)
                    .ConfigureAwait(false);
                stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                RecordFailure(activity, exception, operation);
                throw;
            }

            onResponseHeaders?.Invoke(response.Headers);

            // Manual enumeration so a mid-stream failure is still recorded on the activity/log (a catch block
            // cannot wrap a yield return).
            await using var events = HermesAgentSse.EnumerateAsync(stream, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                bool moved;
                try
                {
                    moved = await events.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    RecordFailure(activity, exception, operation);
                    throw;
                }

                if (!moved) break;
                yield return events.Current;
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            HermesAgentClientLog.OperationSucceeded(logger, operation);
        }
    }

    /// <summary>
    ///     Builds a request URI with a query string, skipping <c>null</c>/whitespace values and URL-encoding the
    ///     rest.
    /// </summary>
    protected static string BuildQuery(string path, params (string Key, string? Value)[] parameters)
    {
        var parts = new List<string>(parameters.Length);
        foreach (var (key, value) in parameters)
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{key}={Uri.EscapeDataString(value)}");

        return parts.Count == 0 ? path : $"{path}?{string.Join('&', parts)}";
    }

    /// <summary>Formats an optional integer query value (invariant culture; <c>null</c> is omitted).</summary>
    protected static string? QueryInt(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    /// <summary>Formats an optional boolean query value as <c>true</c>/<c>false</c> (<c>null</c> is omitted).</summary>
    protected static string? QueryBool(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => null
    };

    private void RecordFailure(Activity? activity, Exception exception, string operation)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.AddException(exception);

        // Cancellation is caller-initiated, not a Hermes Agent failure — trace it, but do not warn.
        if (exception is HermesAgentApiException apiException)
            HermesAgentClientLog.OperationFailedWithStatus(logger, exception, operation,
                (int)apiException.StatusCode);
        else if (exception is not OperationCanceledException)
            HermesAgentClientLog.OperationFailed(logger, exception, operation);
    }
}
