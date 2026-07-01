using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk;

/// <summary>
///     Base class for resource-area API implementations. Centralizes the GET-and-deserialize flow with a Zendesk
///     domain <see cref="Activity" /> (status + exception), non-success handling (<see cref="ZendeskApiException" />)
///     and debug logging.
/// </summary>
internal abstract class ZendeskResourceApi(HttpClient httpClient, ILogger logger)
{
    /// <summary>
    ///     The shared Zendesk <see cref="HttpClient" /> (base address + auth handler), for subclasses that
    ///     need raw access beyond <see cref="GetAsync{TResponse}" /> (e.g. streaming attachment content).
    /// </summary>
    protected HttpClient HttpClient { get; } = httpClient;

    protected async Task<TResponse> GetAsync<TResponse>(string requestUri, string operation,
        CancellationToken cancellationToken)
    {
        using var activity = ZendeskClientInstrumentation.ActivitySource
            .StartActivity(operation, ActivityKind.Client);
        activity?.SetTag("zendesk.operation", operation);

        try
        {
            using var response = await HttpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            await ZendeskResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            var payload = await response.Content
                .ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false);
            if (payload is null)
                throw new InvalidOperationException($"Zendesk returned an empty response for '{operation}'.");

            activity?.SetStatus(ActivityStatusCode.Ok);
            logger.LogDebug("Zendesk {Operation} succeeded", operation);
            return payload;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            throw;
        }
    }
}