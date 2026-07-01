namespace ES.FX.Zendesk.MCP.Host.Execution;

/// <summary>
///     Convenience helpers for interpreting an <see cref="McpExecutionMode" />.
/// </summary>
public static class McpExecutionModeExtensions
{
    /// <summary>
    ///     Whether write operations should actually be performed (only in <see cref="McpExecutionMode.Default" />).
    /// </summary>
    public static bool AllowsWrites(this McpExecutionMode mode) => mode == McpExecutionMode.Default;

    /// <summary>
    ///     Whether write operations should be simulated without performing any changes.
    /// </summary>
    public static bool IsDryRun(this McpExecutionMode mode) => mode == McpExecutionMode.DryRun;

    /// <summary>
    ///     Whether write operations must be rejected.
    /// </summary>
    public static bool IsReadOnly(this McpExecutionMode mode) => mode == McpExecutionMode.ReadOnly;
}