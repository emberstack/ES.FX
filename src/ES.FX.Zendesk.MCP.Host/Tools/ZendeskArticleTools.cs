using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for the Zendesk Help Center knowledge base. Namespaced <c>zendesk_articles_*</c>.
/// </summary>
[McpServerToolType]
public sealed class ZendeskArticleTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Full-text searches Help Center knowledge base articles.</summary>
    [McpServerTool(Name = "zendesk_articles_search", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Full-text searches the Help Center knowledge base for articles that answer a customer's question — the " +
        "primary way to find an existing answer to resolve or deflect a ticket. Returns relevance-ranked results " +
        "with snippets; call zendesk_articles_read for the full article body. Read-only.")]
    public Task<ZendeskArticleSearchResults> Search(
        [Description("The customer's question or keywords to search for.")]
        string query,
        [Description("Optional locale to scope results, e.g. \"en-us\".")]
        string? locale = null,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description("Results per page (default 25, max 100).")]
        int? perPage = 25,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Articles.SearchAsync(query, locale, page, perPage, cancellationToken));

    /// <summary>Returns a single Help Center article including its full body.</summary>
    [McpServerTool(Name = "zendesk_articles_read", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single Help Center article including its full body (HTML). Use after zendesk_articles_search to " +
        "read the complete answer, extract steps, and quote/summarize it back to the customer. Read-only.")]
    public Task<ZendeskArticle> Read(
        [Description("The numeric Help Center article id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Articles.GetByIdAsync(id, cancellationToken));
}