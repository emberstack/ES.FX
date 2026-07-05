using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ES.FX.NousResearch.HermesAgent.Abstractions.Models;

namespace ES.FX.NousResearch.HermesAgent;

/// <summary>
///     Turns non-success Hermes Agent responses into a <see cref="HermesAgentApiException" /> that preserves the
///     status code, the parsed error object, the <c>Retry-After</c> hint (concurrency cap) and a bounded prefix
///     of the response body for diagnostics.
/// </summary>
internal static class HermesAgentResponseGuard
{
    private const int MaxBodyBytes = 2048;

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await ReadBodyPrefixAsync(response.Content, cancellationToken).ConfigureAwait(false);

        throw new HermesAgentApiException(
            response.StatusCode,
            string.IsNullOrWhiteSpace(body) ? null : body,
            $"The Hermes Agent API request failed with status {(int)response.StatusCode} ({response.StatusCode}).")
        {
            Error = ParseError(body),
            RetryAfter = ResolveRetryAfter(response.Headers.RetryAfter)
        };
    }

    /// <summary>
    ///     Parses the error body into a <see cref="HermesAgentError" />. Handles both the OpenAI-style envelope
    ///     <c>{"error": {"message", "type", "param", "code"}}</c> (all <c>/v1</c> endpoints) and the jobs API's
    ///     flat <c>{"error": "&lt;string&gt;"}</c> shape (mapped with only <see cref="HermesAgentError.Message" />
    ///     set). Best-effort: a truncated or non-JSON body yields <c>null</c> — the raw prefix stays available on
    ///     <see cref="HermesAgentApiException.ResponseBody" />.
    /// </summary>
    private static HermesAgentError? ParseError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("error", out var error))
                return null;

            return error.ValueKind switch
            {
                JsonValueKind.Object => error.Deserialize<HermesAgentError>(),
                JsonValueKind.String => new HermesAgentError { Message = error.GetString() },
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Reads at most <see cref="MaxBodyBytes" /> of the error body, so an unbounded error response cannot
    ///     balloon memory. Best-effort: a failure while reading the body must not mask the status-carrying
    ///     exception.
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