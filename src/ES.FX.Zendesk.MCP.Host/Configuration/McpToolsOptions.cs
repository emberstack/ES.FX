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
    ///     The set of tool areas to register. An <em>empty</em> (or absent) list registers <em>all</em> areas
    ///     (backward compatible). When non-empty, only tool classes whose area is in this set are registered; an
    ///     unknown/misspelled area — or a list that is present but entirely blank — fails startup (fail-closed)
    ///     rather than silently exposing nothing (or everything). Blank entries mixed with valid ones are ignored.
    ///     Matching is case-insensitive; areas are the plural snake_case resource names (e.g. <c>tickets</c>,
    ///     <c>ticket_fields</c>).
    /// </summary>
    public string[] Areas { get; set; } = [];
}
