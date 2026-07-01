using ES.FX.Zendesk.MCP.Host.Execution;

namespace ES.FX.Zendesk.MCP.Host.Configuration;

/// <summary>
///     Execution-mode options controlling whether write operations perform changes, are simulated (dry-run), or
///     are rejected (read-only).
/// </summary>
public class McpExecutionOptions
{
    /// <summary>
    ///     The configured baseline execution mode. Defaults to <see cref="McpExecutionMode.Default" />.
    /// </summary>
    public McpExecutionMode Mode { get; set; } = McpExecutionMode.Default;

    /// <summary>
    ///     When <c>true</c>, a per-request header (<see cref="HeaderName" />) may further restrict the mode. A header
    ///     can only ever <em>tighten</em> the mode, never relax the configured baseline. Defaults to <c>true</c>.
    /// </summary>
    public bool AllowHeaderOverride { get; set; } = true;

    /// <summary>
    ///     The name of the request header used to request a (more restrictive) execution mode.
    ///     Defaults to <c>X-Mcp-Execution-Mode</c>.
    /// </summary>
    public string HeaderName { get; set; } = "X-Mcp-Execution-Mode";
}