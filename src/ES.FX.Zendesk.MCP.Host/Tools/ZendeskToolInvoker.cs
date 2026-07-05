using System.Globalization;
using ES.FX.Zendesk.MCP.Host.Execution;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     Invokes a Zendesk API-client call on behalf of an MCP tool and surfaces Zendesk failures back to the
///     calling agent.
/// </summary>
/// <remarks>
///     The MCP SDK catches any exception thrown by a tool and, unless it is an <see cref="McpException" />,
///     replaces it with an opaque <c>"An error occurred invoking '{tool}'."</c> result — discarding the HTTP
///     status code and the Zendesk error body carried by <see cref="ZendeskApiException" />. Routing tool calls
///     through here re-throws a <see cref="ZendeskApiException" /> as an <see cref="McpException" />, whose message
///     the SDK surfaces verbatim, so the agent can distinguish (for example) <c>404 Not Found</c> from
///     <c>403 Forbidden</c> from <c>422</c> and self-correct. When the exception carries a
///     <see cref="ZendeskApiException.RetryAfter" /> delay (typically a <c>429</c>), the message includes an
///     explicit <c>"Retry after N seconds."</c> hint. Only the typed <see cref="ZendeskApiException" /> is
///     translated; other exceptions keep their default (generic) SDK handling.
/// </remarks>
internal static class ZendeskToolInvoker
{
    public static async Task<T> InvokeAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (ZendeskApiException exception)
        {
            throw new McpException(Describe(exception));
        }
        catch (ApiException exception)
        {
            throw new McpException(Describe(exception));
        }
    }

    /// <summary>
    ///     Invokes a write (mutating) Zendesk operation, honoring the effective execution mode: in
    ///     <see cref="McpExecutionMode.ReadOnly" /> the call is rejected with an <see cref="McpException" />; in
    ///     <see cref="McpExecutionMode.DryRun" /> nothing is sent to Zendesk and an explicit
    ///     <see cref="ZendeskDryRunResult" /> is returned; otherwise the operation executes and its result is
    ///     returned. Every write tool must route through here — this is the single choke point that makes the
    ///     execution-mode guarantees hold.
    /// </summary>
    /// <param name="executionMode">The per-request execution-mode accessor.</param>
    /// <param name="action">
    ///     The action in the infinitive, used in mode messages (for example <c>"create a ticket with subject 'X'"</c>).
    /// </param>
    /// <param name="operation">The client call performing the write.</param>
    /// <param name="request">The request payload, echoed back in dry-run results for inspection.</param>
    public static async Task<object> InvokeWriteAsync<T>(IMcpExecutionModeAccessor executionMode, string action,
        Func<Task<T>> operation, object? request = null) where T : notnull
    {
        var mode = executionMode.EffectiveMode;
        if (mode.IsReadOnly()) throw ReadOnlyRejection(action);
        if (mode.IsDryRun()) return DryRun(action, request);
        return await InvokeAsync(operation).ConfigureAwait(false);
    }

    /// <summary>
    ///     Variant of <see cref="InvokeWriteAsync{T}(IMcpExecutionModeAccessor, string, Func{Task{T}}, object)" />
    ///     for write tools that build their own dry-run result — bulk (<c>*_many</c>) tools use it to emit the
    ///     <see cref="ZendeskDryRunResult.ForBulk" /> digest instead of echoing up to 100 write models verbatim.
    ///     The factory runs only in dry-run mode, so the digest costs nothing on real writes.
    /// </summary>
    /// <param name="executionMode">The per-request execution-mode accessor.</param>
    /// <param name="action">The action in the infinitive, used in mode messages.</param>
    /// <param name="operation">The client call performing the write.</param>
    /// <param name="dryRun">Builds the <see cref="ZendeskDryRunResult" /> returned in dry-run mode.</param>
    public static async Task<object> InvokeWriteAsync<T>(IMcpExecutionModeAccessor executionMode, string action,
        Func<Task<T>> operation, Func<ZendeskDryRunResult> dryRun) where T : notnull
    {
        var mode = executionMode.EffectiveMode;
        if (mode.IsReadOnly()) throw ReadOnlyRejection(action);
        if (mode.IsDryRun()) return dryRun();
        return await InvokeAsync(operation).ConfigureAwait(false);
    }

    /// <summary>
    ///     Invokes a write (mutating) Zendesk operation whose API response has no body (for example a delete or
    ///     restore returning <c>204</c>), honoring the effective execution mode like
    ///     <see cref="InvokeWriteAsync{T}(IMcpExecutionModeAccessor, string, Func{Task{T}}, object)" /> and
    ///     returning a <see cref="ZendeskWriteAcknowledgement" /> on success.
    /// </summary>
    public static async Task<object> InvokeWriteAsync(IMcpExecutionModeAccessor executionMode, string action,
        Func<Task> operation, object? request = null)
    {
        var mode = executionMode.EffectiveMode;
        if (mode.IsReadOnly()) throw ReadOnlyRejection(action);
        if (mode.IsDryRun()) return DryRun(action, request);

        await InvokeAsync(async () =>
        {
            await operation().ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
        return new ZendeskWriteAcknowledgement { Description = $"Zendesk accepted the request to {action}." };
    }

    private static McpException ReadOnlyRejection(string action) => new(
        $"Rejected: the server is in read-only execution mode, so write tools are disabled. The request to {action} was NOT performed.");

    private static ZendeskDryRunResult DryRun(string action, object? request) => new()
    {
        Description = $"Dry run — no changes were made. This call would {action}.",
        Request = request
    };

    private static string Describe(ZendeskApiException exception)
    {
        var message =
            $"The Zendesk API request failed with status {(int)exception.StatusCode} ({exception.StatusCode}).";
        if (exception.RetryAfter is { } retryAfter)
            message = $"{message} Retry after {(long)Math.Ceiling(retryAfter.TotalSeconds)} seconds.";
        return string.IsNullOrWhiteSpace(exception.ResponseBody)
            ? message
            : $"{message} Zendesk response: {exception.ResponseBody}";
    }

    /// <summary>
    ///     Describes a failure surfaced by the Kiota request adapter. Non-retryable 4xx errors are already
    ///     translated to rich <see cref="ZendeskApiException" />s by the response-guard handler before Kiota sees
    ///     them, so this path covers retry-exhausted statuses (408/429/5xx) — where the <c>Retry-After</c> hint in
    ///     the response headers is the actionable detail.
    /// </summary>
    private static string Describe(ApiException exception)
    {
        var message = $"The Zendesk API request failed with status {exception.ResponseStatusCode}.";
        if (ResolveRetryAfter(exception) is { } retryAfter)
            message = $"{message} Retry after {(long)Math.Ceiling(retryAfter.TotalSeconds)} seconds.";
        return message;
    }

    private static TimeSpan? ResolveRetryAfter(ApiException exception)
    {
        var values = exception.ResponseHeaders?
            .FirstOrDefault(header => string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
            .Value;
        var value = values?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (long.TryParse(value, out var seconds))
            return seconds >= 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
        // Invariant culture: RFC 1123 HTTP-dates must parse identically regardless of the host locale.
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var date))
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait >= TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }
}