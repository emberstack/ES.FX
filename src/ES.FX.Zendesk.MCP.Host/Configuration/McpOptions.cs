namespace ES.FX.Zendesk.MCP.Host.Configuration;

/// <summary>
///     Options for the MCP server hosted by this application.
/// </summary>
public class McpOptions
{
    /// <summary>
    ///     The configuration section these options bind to.
    /// </summary>
    public const string SectionKey = "Mcp";

    /// <summary>
    ///     The route pattern the MCP endpoints are mapped to. Defaults to an empty string (mapped at the application root).
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    ///     When <c>true</c> the Streamable HTTP transport runs statelessly (no <c>Mcp-Session-Id</c>, horizontally
    ///     scalable). Defaults to <c>true</c>.
    /// </summary>
    public bool Stateless { get; set; } = true;

    /// <summary>
    ///     Execution-mode options (read-only / dry-run) for write operations.
    /// </summary>
    public McpExecutionOptions Execution { get; set; } = new();
}