using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Articles;

/// <summary>
///     Default <see cref="IZendeskArticlesApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskArticlesApi(HttpClient httpClient, ILogger<ZendeskArticlesApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskArticlesApi
{
    /// <inheritdoc />
    public Task<ZendeskArticleSearchResults> SearchAsync(string query, string? locale = null, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("help_center/articles/search.json",
            ("query", query), ("locale", locale), ("page", ZendeskQuery.Int(page)),
            ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskArticleSearchResults>(requestUri, "Zendesk.Articles.Search", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskArticle> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskArticleResponse>($"help_center/articles/{id}.json", "Zendesk.Articles.Get",
            cancellationToken).ConfigureAwait(false);
        return response.Article ?? throw new InvalidOperationException($"Zendesk article '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskArticlesResult> ListAsync(string? locale = null, long? sectionId = null, int? pageSize = null,
        string? afterCursor = null, IReadOnlyList<string>? include = null,
        CancellationToken cancellationToken = default)
    {
        var suffix = sectionId is null ? "articles.json" : $"sections/{sectionId}/articles.json";
        var requestUri = ZendeskQuery.Build(HelpCenterPath(locale, suffix),
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskArticlesResult>(requestUri, "Zendesk.Articles.List", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskHelpCenterSectionsResult> ListSectionsAsync(string? locale = null, long? categoryId = null,
        int? page = null, int? perPage = null, CancellationToken cancellationToken = default)
    {
        var suffix = categoryId is null ? "sections.json" : $"categories/{categoryId}/sections.json";
        var requestUri = ZendeskQuery.Build(HelpCenterPath(locale, suffix),
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskHelpCenterSectionsResult>(requestUri, "Zendesk.Articles.Sections", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskHelpCenterSection> GetSectionByIdAsync(long id, string? locale = null,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskHelpCenterSectionResponse>(
                HelpCenterPath(locale, $"sections/{id}.json"), "Zendesk.Articles.Section", cancellationToken)
            .ConfigureAwait(false);
        return response.Section
               ?? throw new InvalidOperationException($"Zendesk Help Center section '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskHelpCenterCategoriesResult> ListCategoriesAsync(string? locale = null, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build(HelpCenterPath(locale, "categories.json"),
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskHelpCenterCategoriesResult>(requestUri, "Zendesk.Articles.Categories",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskHelpCenterCategory> GetCategoryByIdAsync(long id, string? locale = null,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskHelpCenterCategoryResponse>(
                HelpCenterPath(locale, $"categories/{id}.json"), "Zendesk.Articles.Category", cancellationToken)
            .ConfigureAwait(false);
        return response.Category
               ?? throw new InvalidOperationException($"Zendesk Help Center category '{id}' was not found.");
    }

    // The {locale} path segment is optional for agents; when supplied it must be escaped (it is caller input).
    private static string HelpCenterPath(string? locale, string suffix) =>
        string.IsNullOrWhiteSpace(locale)
            ? $"help_center/{suffix}"
            : $"help_center/{Uri.EscapeDataString(locale)}/{suffix}";
}