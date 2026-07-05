using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk tags (account-wide tag usage). Namespaced <c>tags_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskTagTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists the most popular Zendesk tags with usage counts.</summary>
    [McpServerTool(Name = "tags_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the most popular Zendesk tags of the last 60 days with usage counts (up to 20,000 tags, decreasing " +
        "popularity; subject to its own rate limit). Prefer cursor pagination (pass pageSize/afterCursor; the " +
        "result's meta.has_more/meta.after_cursor drive continuation) — offset paging (page/perPage; " +
        "count/next_page indicate more pages) is capped at 10,000 records, leaving the tail unreachable. Read-only.")]
    public Task<ZendeskTagsResult> List(
        [Description("The 1-based page number for offset pagination (optional; capped at 10,000 records).")]
        int? page = null,
        [Description("Results per page for offset pagination (optional, max 100).")]
        int? perPage = null,
        [Description("The cursor page size (optional, max 100 records per page). Preferred over page/perPage.")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (omit for the first page).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tags.ListAsync(page: page, perPage: perPage, pageSize: pageSize,
                afterCursor: afterCursor, cancellationToken: cancellationToken));

    /// <summary>Returns the account-wide tag count.</summary>
    [McpServerTool(Name = "tags_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the (cached, approximate) account-wide Zendesk tag count; 'refreshed_at' indicates when the " +
        "cached value was computed. Once the true count exceeds 100,000 the value is refreshed only every 24 hours " +
        "and stays capped at 100,000 until that background update completes, during which 'refreshed_at' may be " +
        "null. Read-only.")]
    public Task<ZendeskCount> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Tags.CountAsync(cancellationToken));

    /// <summary>Suggests Zendesk tag names matching a prefix.</summary>
    [McpServerTool(Name = "tags_autocomplete", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Suggests Zendesk tag names matching a prefix (minimum two characters; up to 15 suggestions drawn ONLY " +
        "from the most-used ticket tags of the last 60 days — a tag that matches the prefix but is outside that top " +
        "set will not appear). Use to find the exact spelling of a tag before searching or tagging. Read-only.")]
    public Task<ZendeskTagNamesResult> Autocomplete(
        [Description(
            "The tag name prefix to complete (minimum 2 characters). Each word within a tag is indexed separately " +
            "(split on underscores, hyphens, spaces, or other punctuation), so a tag matches if the tag itself OR " +
            "any word within it starts with the prefix (e.g. \"trig\" matches \"set_by_this_trigger\" via the word " +
            "\"trigger\").")]
        string name,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Tags.AutocompleteAsync(name, cancellationToken));
}
