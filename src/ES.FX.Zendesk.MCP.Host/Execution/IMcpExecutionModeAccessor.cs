namespace ES.FX.Zendesk.MCP.Host.Execution;

/// <summary>
///     Resolves the execution mode for the current MCP request. Write tools consult this to decide whether a
///     write operation may proceed, must be simulated (dry-run), or must be rejected (read-only).
/// </summary>
public interface IMcpExecutionModeAccessor
{
    /// <summary>
    ///     The configured baseline execution mode (ignores any per-request override).
    /// </summary>
    McpExecutionMode ConfiguredMode { get; }

    /// <summary>
    ///     The effective execution mode for the current request: the configured baseline, possibly further
    ///     restricted by a request header. Never less restrictive than <see cref="ConfiguredMode" />.
    /// </summary>
    McpExecutionMode EffectiveMode { get; }
}