namespace ES.FX.Zendesk;

/// <summary>
///     Turns non-success Zendesk responses into a <see cref="ZendeskApiException" /> that preserves the status
///     code and (truncated) response body for diagnostics.
/// </summary>
internal static class ZendeskResponseGuard
{
    private const int MaxBodyLength = 2048;

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (body.Length > MaxBodyLength) body = body[..MaxBodyLength];

        throw new ZendeskApiException(
            response.StatusCode,
            string.IsNullOrWhiteSpace(body) ? null : body,
            $"The Zendesk API request failed with status {(int)response.StatusCode} ({response.StatusCode}).");
    }
}