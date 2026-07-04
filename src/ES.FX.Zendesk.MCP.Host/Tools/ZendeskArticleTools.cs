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
        [Description(
            "Results per page (default 25, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
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

    /// <summary>Lists Help Center articles, optionally scoped to a section.</summary>
    [McpServerTool(Name = "zendesk_articles_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Help Center articles, optionally scoped to a section — use to browse the knowledge base structurally " +
        "(rather than by relevance, which is zendesk_articles_search). Cursor pagination: pass pageSize/afterCursor; " +
        "the result's meta.has_more/meta.after_cursor drive continuation. Read-only.")]
    public Task<ZendeskArticlesResult> List(
        [Description("Optional locale to scope results, e.g. \"en-us\".")]
        string? locale = null,
        [Description("When set, lists only the articles in this section (see zendesk_articles_sections).")]
        long? sectionId = null,
        [Description("The cursor page size (max 100).")]
        int? pageSize = null,
        [Description("The cursor from the previous page's meta.after_cursor (optional).")]
        string? afterCursor = null,
        [Description("Sideloads resolved inline as sibling arrays: \"users\", \"sections\", \"categories\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Articles.ListAsync(locale, sectionId, pageSize, afterCursor, include: include,
                cancellationToken: cancellationToken));

    /// <summary>Lists Help Center sections, optionally scoped to a category.</summary>
    [McpServerTool(Name = "zendesk_articles_sections", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Help Center sections (the middle tier of the category → section → article hierarchy), optionally " +
        "scoped to a category — use the returned section ids to browse articles via zendesk_articles_list. " +
        "Read-only.")]
    public Task<ZendeskHelpCenterSectionsResult> Sections(
        [Description("Optional locale to scope results, e.g. \"en-us\".")]
        string? locale = null,
        [Description("When set, lists only the sections in this category (see zendesk_articles_categories).")]
        long? categoryId = null,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 100,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Articles.ListSectionsAsync(locale, categoryId, page, perPage, cancellationToken));

    /// <summary>Returns a single Help Center section by id.</summary>
    [McpServerTool(Name = "zendesk_articles_section_read", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single Help Center section by id — the name/description behind an article's section_id. " +
        "Read-only.")]
    public Task<ZendeskHelpCenterSection> SectionRead(
        [Description("The numeric Help Center section id.")]
        long id,
        [Description("Optional locale segment, e.g. \"en-us\".")]
        string? locale = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Articles.GetSectionByIdAsync(id, locale, cancellationToken));

    /// <summary>Lists Help Center categories.</summary>
    [McpServerTool(Name = "zendesk_articles_categories", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists Help Center categories (the top tier of the category → section → article hierarchy) — use the " +
        "returned category ids to browse sections via zendesk_articles_sections. Read-only.")]
    public Task<ZendeskHelpCenterCategoriesResult> Categories(
        [Description("Optional locale to scope results, e.g. \"en-us\".")]
        string? locale = null,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = 100,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Articles.ListCategoriesAsync(locale, page, perPage, cancellationToken));

    /// <summary>Returns a single Help Center category by id.</summary>
    [McpServerTool(Name = "zendesk_articles_category_read", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single Help Center category by id — the name/description behind a section's category_id. " +
        "Read-only.")]
    public Task<ZendeskHelpCenterCategory> CategoryRead(
        [Description("The numeric Help Center category id.")]
        long id,
        [Description("Optional locale segment, e.g. \"en-us\".")]
        string? locale = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Articles.GetCategoryByIdAsync(id, locale, cancellationToken));
}