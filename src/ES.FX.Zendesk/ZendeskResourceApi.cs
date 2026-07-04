using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk;

/// <summary>
///     Base class for resource-area API implementations. Centralizes the request-and-deserialize flow with a
///     Zendesk domain <see cref="Activity" /> (status + exception), non-success handling
///     (<see cref="ZendeskApiException" />) and logging.
/// </summary>
internal abstract class ZendeskResourceApi(HttpClient httpClient, ILogger logger)
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
    ///     The shared Zendesk <see cref="HttpClient" /> (base address + auth handler), for subclasses that
    ///     need raw access beyond the helpers (e.g. streaming attachment content).
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    protected Task<TResponse> GetAsync<TResponse>(string requestUri, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Get, requestUri, null, operation, cancellationToken);

    protected Task<TResponse> PostAsync<TResponse>(string requestUri, object payload, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Post, requestUri,
            JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions), operation, cancellationToken);

    protected Task<TResponse> PutAsync<TResponse>(string requestUri, object payload, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Put, requestUri,
            JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions), operation, cancellationToken);

    /// <summary>Sends a body-less <c>PUT</c> (e.g. restore/recover/make-default style operations).</summary>
    protected Task<TResponse> PutAsync<TResponse>(string requestUri, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Put, requestUri, null, operation, cancellationToken);

    protected Task<TResponse> DeleteAsync<TResponse>(string requestUri, string operation,
        CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Delete, requestUri, null, operation, cancellationToken);

    /// <summary>
    ///     Sends a request whose response is an async-job envelope (<c>{ "job_status": {...} }</c>) and unwraps
    ///     it. <paramref name="payload" /> may be <c>null</c> for query-only bulk operations.
    /// </summary>
    protected async Task<ZendeskJobStatus> SendJobAsync(HttpMethod method, string requestUri, object? payload,
        string operation, CancellationToken cancellationToken)
    {
        var content = payload is null
            ? null
            : JsonContent.Create(payload, payload.GetType(), options: WriteJsonOptions);
        var response = await SendAsync<ZendeskJobStatusResponse>(method, requestUri, content, operation,
            cancellationToken).ConfigureAwait(false);
        return response.JobStatus
               ?? throw new InvalidOperationException($"Zendesk returned no job status for '{operation}'.");
    }

    /// <summary>Validates a bulk-operation item count (Zendesk accepts 1–100 items per bulk request).</summary>
    protected static void ValidateBulkCount(int count, string paramName)
    {
        if (count is 0 or > 100)
            throw new ArgumentException("Zendesk bulk operations accept between 1 and 100 items.", paramName);
    }

    /// <summary>Sends a request whose success response carries no payload the caller needs (e.g. <c>204</c> deletes).</summary>
    protected async Task SendAsync(HttpMethod method, string requestUri, HttpContent? content, string operation,
        CancellationToken cancellationToken)
    {
        using var activity = ZendeskClientInstrumentation.ActivitySource
            .StartActivity(operation, ActivityKind.Client);
        activity?.SetTag("zendesk.operation", operation);

        try
        {
            using var request = new HttpRequestMessage(method, requestUri) { Content = content };
            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await ZendeskResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            activity?.SetStatus(ActivityStatusCode.Ok);
            ZendeskClientLog.OperationSucceeded(logger, operation);
        }
        catch (Exception exception)
        {
            RecordFailure(activity, exception, operation);
            throw;
        }
    }

    protected async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string requestUri, HttpContent? content,
        string operation, CancellationToken cancellationToken)
    {
        using var activity = ZendeskClientInstrumentation.ActivitySource
            .StartActivity(operation, ActivityKind.Client);
        activity?.SetTag("zendesk.operation", operation);

        try
        {
            using var request = new HttpRequestMessage(method, requestUri) { Content = content };
            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            await ZendeskResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            var payload = await response.Content
                .ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false);
            if (payload is null)
                throw new InvalidOperationException($"Zendesk returned an empty response for '{operation}'.");

            activity?.SetStatus(ActivityStatusCode.Ok);
            ZendeskClientLog.OperationSucceeded(logger, operation);
            return payload;
        }
        catch (Exception exception)
        {
            RecordFailure(activity, exception, operation);
            throw;
        }
    }

    private void RecordFailure(Activity? activity, Exception exception, string operation)
    {
        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.AddException(exception);

        // Cancellation is caller-initiated, not a Zendesk failure — trace it, but do not warn.
        if (exception is ZendeskApiException apiException)
            ZendeskClientLog.OperationFailedWithStatus(logger, exception, operation, (int)apiException.StatusCode);
        else if (exception is not OperationCanceledException)
            ZendeskClientLog.OperationFailed(logger, exception, operation);
    }
}