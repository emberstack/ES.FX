using System.Net.Http.Headers;
using System.Text;

namespace ES.FX.Zendesk;

/// <summary>
///     Turns non-success Zendesk responses into a <see cref="ZendeskApiException" /> that preserves the status
///     code, the <c>Retry-After</c> hint (rate limiting) and a bounded prefix of the response body for diagnostics.
///     Public so callers that receive the raw <see cref="HttpResponseMessage" /> themselves (for example via a
///     Kiota <c>NativeResponseHandler</c>, which bypasses the adapter's error mapping) can apply the exact same
///     error semantics as the <see cref="ZendeskResponseGuardHandler" /> in the HTTP handler chain.
/// </summary>
public static class ZendeskResponseGuard
{
    private const int MaxBodyBytes = 2048;

    /// <summary>
    ///     Returns when <paramref name="response" /> is a success; otherwise throws a
    ///     <see cref="ZendeskApiException" /> carrying the status code, at most 2 KiB of the response body and
    ///     the <c>Retry-After</c> delay (when present).
    /// </summary>
    /// <param name="response">The Zendesk response to check.</param>
    /// <param name="cancellationToken">A token to cancel reading the error body.</param>
    /// <exception cref="ZendeskApiException">The response has a non-success status code.</exception>
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await ReadBodyPrefixAsync(response.Content, cancellationToken).ConfigureAwait(false);

        throw new ZendeskApiException(
            response.StatusCode,
            string.IsNullOrWhiteSpace(body) ? null : body,
            $"The Zendesk API request failed with status {(int)response.StatusCode} ({response.StatusCode}).")
        {
            RetryAfter = ResolveRetryAfter(response.Headers.RetryAfter)
        };
    }

    /// <summary>
    ///     Reads at most <see cref="MaxBodyBytes" /> of the error body, so an unbounded (or streamed, e.g. the
    ///     attachment download path) error response cannot balloon memory. Best-effort: a failure while reading the
    ///     body must not mask the status-carrying exception.
    /// </summary>
    private static async Task<string?> ReadBodyPrefixAsync(HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var buffer = new byte[MaxBodyBytes];
            var total = 0;
            int read;
            while (total < buffer.Length &&
                   (read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false)) >
                   0)
                total += read;

            return Encoding.UTF8.GetString(buffer, 0, total);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static TimeSpan? ResolveRetryAfter(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter is null) return null;
        if (retryAfter.Delta is { } delta) return delta >= TimeSpan.Zero ? delta : TimeSpan.Zero;
        if (retryAfter.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait >= TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }
}