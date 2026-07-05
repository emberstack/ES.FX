using System.Net;

namespace ES.FX.Zendesk;

/// <summary>
///     Innermost delegating handler that turns non-success Zendesk responses into a typed
///     <see cref="ZendeskApiException" /> (status code, bounded body prefix, <c>Retry-After</c>) so the rich error
///     detail survives the Kiota request adapter, which would otherwise discard the response body.
/// </summary>
/// <remarks>
///     Statuses the standard resilience pipeline retries (<c>408</c>, <c>429</c>, <c>5xx</c>) are deliberately
///     passed through untouched: this handler sits inside the resilience handler, so throwing here would defeat
///     the retry policy. When retries are exhausted, those responses surface as the Kiota
///     <see cref="Microsoft.Kiota.Abstractions.ApiException" /> (status code and headers, including
///     <c>Retry-After</c>, preserved) instead.
/// </remarks>
public sealed class ZendeskResponseGuardHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode || IsRetriedByResilience(response.StatusCode)) return response;

        using (response)
        {
            await ZendeskResponseGuard.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Unreachable: the response guard throws for non-success statuses.");
        }
    }

    private static bool IsRetriedByResilience(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}