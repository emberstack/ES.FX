namespace ES.FX.Zendesk.MCP.Host.Execution;

/// <summary>
///     Pure resolution logic for combining the configured baseline execution mode with an optional
///     per-request override. The override can only ever <em>tighten</em> the mode (never relax it), so a
///     header can never be used to escalate privileges beyond the configured baseline.
/// </summary>
public static class McpExecutionModeResolver
{
    /// <summary>
    ///     Resolves the effective execution mode.
    /// </summary>
    /// <param name="configured">The configured baseline mode.</param>
    /// <param name="requestedHeaderValue">The raw value of the request override header, if any.</param>
    /// <param name="allowOverride">Whether a request override is permitted at all.</param>
    /// <returns>
    ///     The more restrictive of the configured baseline and the requested mode. A requested mode that is less
    ///     restrictive than the baseline (or an unrecognized value, or a disabled override) yields the baseline.
    /// </returns>
    public static McpExecutionMode Resolve(McpExecutionMode configured, string? requestedHeaderValue,
        bool allowOverride)
    {
        if (!allowOverride) return configured;
        if (!TryParse(requestedHeaderValue, out var requested)) return configured;

        // Higher ordinal == more restrictive. Only ever move to a more restrictive mode.
        return (McpExecutionMode)Math.Max((int)configured, (int)requested);
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