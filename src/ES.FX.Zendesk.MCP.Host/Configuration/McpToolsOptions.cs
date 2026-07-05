namespace ES.FX.Zendesk.MCP.Host.Configuration;

/// <summary>
///     Options controlling which Zendesk tool <em>areas</em> (top-level resource groupings such as
///     <c>tickets</c>, <c>users</c>, <c>organizations</c>) are exposed by this MCP server. This is a
///     server-side filtering lever: because MCP clients (Hermes, Claude) filter by exact tool name only,
///     narrowing the surface here — rather than enumerating names in a client include-list — is the primary
///     way to expose "only ticket operations" and, combined with a read-only execution baseline, "only ticket
///     reads".
/// </summary>
public class McpToolsOptions
{
    /// <summary>
    ///     The smallest accepted <see cref="MaxResponseChars" /> value (default or per-tool): anything lower
    ///     cannot fit even a bare envelope plus its recovery note, so it is a misconfiguration and fails startup.
    /// </summary>
    public const int MinimumMaxResponseChars = 1000;

    /// <summary>
    ///     The set of tool areas to register. An <em>empty</em> (or absent) list registers <em>all</em> areas
    ///     (backward compatible). When non-empty, only tool classes whose area is in this set are registered; an
    ///     unknown/misspelled area — or a list that is present but entirely blank — fails startup (fail-closed)
    ///     rather than silently exposing nothing (or everything). Blank entries mixed with valid ones are ignored.
    ///     Matching is case-insensitive; areas are the plural snake_case resource names (e.g. <c>tickets</c>,
    ///     <c>ticket_fields</c>).
    /// </summary>
    public string[] Areas { get; set; } = [];

    /// <summary>
    ///     The response-size budget, in serialized JSON characters, applied where tool responses are built (a
    ///     safety net — projection keeps typical responses far below it). List responses over the budget drop
    ///     tail items and explain how to recover in their <c>note</c>; non-list responses over it fail with an
    ///     actionable error. Tools with their own explicit size caps (e.g. <c>attachments_get</c>'s byte cap)
    ///     are exempt by design.
    /// </summary>
    public int MaxResponseChars { get; set; } = 60_000;

    /// <summary>
    ///     Per-tool overrides of <see cref="MaxResponseChars" />, keyed by tool name (case-insensitive), e.g.
    ///     <c>Mcp:Tools:MaxResponseCharsByTool:tickets_audits_list = 90000</c>. Use these to raise (or tighten)
    ///     the budget for a specific tool without moving the global default.
    /// </summary>
    public Dictionary<string, int> MaxResponseCharsByTool { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Resolves the effective response-size budget for a tool: its
    ///     <see cref="MaxResponseCharsByTool" /> override when configured, otherwise
    ///     <see cref="MaxResponseChars" />.
    /// </summary>
    public int GetMaxResponseChars(string toolName) =>
        MaxResponseCharsByTool.TryGetValue(toolName, out var maxResponseChars)
            ? maxResponseChars
            : MaxResponseChars;
}