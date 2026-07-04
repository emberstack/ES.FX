namespace ES.FX.Zendesk.MCP.Host.Execution;

/// <summary>
///     The execution mode of the MCP server, controlling whether mutating (write) tool operations are
///     permitted. Values are ordered from least to most restrictive; a request may only ever move to a more
///     restrictive mode, never a less restrictive one.
/// </summary>
public enum McpExecutionMode
{
    /// <summary>
    ///     All operations execute normally. Write operations perform their changes.
    /// </summary>
    Default = 0,

    /// <summary>
    ///     Write operations are accepted but not executed; instead they return an explicit dry-run result
    ///     describing the change that would have been made. Read operations execute normally.
    /// </summary>
    DryRun = 1,

    /// <summary>
    ///     Write operations are rejected. Only read operations are permitted.
    /// </summary>
    ReadOnly = 2
}