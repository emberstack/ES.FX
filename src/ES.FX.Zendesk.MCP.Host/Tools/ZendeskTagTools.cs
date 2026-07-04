using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP read tools for Zendesk tags (account-wide tag usage). Namespaced <c>zendesk_tags_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskTagTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Lists the most popular Zendesk tags with usage counts.</summary>
    [McpServerTool(Name = "zendesk_tags_list", ReadOnly = true, OpenWorld = true)]
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
        [Description("The cursor page size (optional, max 100). Preferred over page/perPage.")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (omit for the first page).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tags.ListAsync(page: page, perPage: perPage, pageSize: pageSize,
                afterCursor: afterCursor, cancellationToken: cancellationToken));

    /// <summary>Returns the account-wide tag count.</summary>
    [McpServerTool(Name = "zendesk_tags_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the (cached, approximate) account-wide Zendesk tag count; 'refreshed_at' indicates when the " +
        "cached value was computed. Read-only.")]
    public Task<ZendeskCount> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Tags.CountAsync(cancellationToken));

    /// <summary>Suggests Zendesk tag names matching a prefix.</summary>
    [McpServerTool(Name = "zendesk_tags_autocomplete", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Suggests Zendesk tag names matching a prefix (minimum two characters; up to 15 suggestions drawn from " +
        "the most-used tags of the last 60 days). Use to find the exact spelling of a tag before searching or " +
        "tagging. Read-only.")]
    public Task<ZendeskTagNamesResult> Autocomplete(
        [Description("The tag name prefix to complete (minimum two characters).")]
        string name,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Tags.AutocompleteAsync(name, cancellationToken));
}
