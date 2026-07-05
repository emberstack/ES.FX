namespace ES.FX.Zendesk.MCP.Host.Execution;

/// <summary>
///     Pure resolution logic for combining the configured baseline execution mode with an optional
///     per-request override. The override can only ever <em>tighten</em> the mode (never relax it), so a
///     header can never be used to escalate privileges beyond the configured baseline.
/// </summary>
public static class McpExecutionModeResolver
{
    /// <summary>
    ///     Resolves the effective execution mode from a single raw header value.
    /// </summary>
    /// <param name="configured">The configured baseline mode.</param>
    /// <param name="requestedHeaderValue">The raw value of the request override header, if any.</param>
    /// <param name="allowOverride">Whether a request override is permitted at all.</param>
    /// <returns>
    ///     The more restrictive of the configured baseline and the requested mode(s). See
    ///     <see cref="Resolve(McpExecutionMode, IEnumerable{string?}?, bool)" /> for the full semantics.
    /// </returns>
    public static McpExecutionMode Resolve(McpExecutionMode configured, string? requestedHeaderValue,
        bool allowOverride)
        => Resolve(configured, [requestedHeaderValue], allowOverride);

    /// <summary>
    ///     Resolves the effective execution mode from all values of the override header. A request may carry the
    ///     header more than once (client retries, proxy injection), and each value may itself be a comma-joined
    ///     list; every token is considered.
    /// </summary>
    /// <param name="configured">The configured baseline mode.</param>
    /// <param name="requestedHeaderValues">All raw values of the request override header, if any.</param>
    /// <param name="allowOverride">Whether a request override is permitted at all.</param>
    /// <returns>
    ///     The most restrictive of the configured baseline and every parseable requested mode. This is a security
    ///     control, so it fails <em>closed</em>: when the header is present but carries any token that cannot be
    ///     parsed, the result is <see cref="McpExecutionMode.ReadOnly" /> — a caller that asked for a restriction we
    ///     cannot understand must not be silently granted the (less restrictive) baseline. An absent or empty header
    ///     (or a disabled override) yields the baseline.
    /// </returns>
    public static McpExecutionMode Resolve(McpExecutionMode configured, IEnumerable<string?>? requestedHeaderValues,
        bool allowOverride)
    {
        if (!allowOverride || requestedHeaderValues is null) return configured;

        var mode = configured;
        foreach (var headerValue in requestedHeaderValues)
        {
            if (string.IsNullOrWhiteSpace(headerValue)) continue;
            foreach (var token in headerValue.Split(',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!TryParse(token, out var requested)) return McpExecutionMode.ReadOnly;

                // Higher ordinal == more restrictive. Only ever move to a more restrictive mode.
                mode = (McpExecutionMode)Math.Max((int)mode, (int)requested);
            }
        }

        return mode;
    }

    /// <summary>
    ///     Attempts to parse an execution mode from a header/string value. Accepts case-insensitive values with
    ///     optional separators, e.g. <c>read-only</c>, <c>readonly</c>, <c>read_only</c>, <c>dry-run</c>, <c>dryrun</c>,
    ///     <c>default</c>/<c>normal</c>.
    /// </summary>
    public static bool TryParse(string? value, out McpExecutionMode mode)
    {
        mode = McpExecutionMode.Default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty);

        switch (normalized)
        {
            case "default":
            case "normal":
                mode = McpExecutionMode.Default;
                return true;
            case "dryrun":
                mode = McpExecutionMode.DryRun;
                return true;
            case "readonly":
                mode = McpExecutionMode.ReadOnly;
                return true;
            default:
                return false;
        }
    }
}