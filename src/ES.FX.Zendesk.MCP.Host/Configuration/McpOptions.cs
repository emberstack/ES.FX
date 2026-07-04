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
    ///     Browser origins allowed to reach the MCP endpoints. Requests that carry an <c>Origin</c> header not on
    ///     this list are rejected with <c>403</c>, as required by the MCP Streamable HTTP transport specification to
    ///     prevent DNS-rebinding attacks. Requests without an <c>Origin</c> header (non-browser clients such as MCP
    ///     agents) are always allowed. Defaults to empty — all browser origins are rejected.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    ///     Execution-mode options (read-only / dry-run) for write operations.
    /// </summary>
    public McpExecutionOptions Execution { get; set; } = new();
}