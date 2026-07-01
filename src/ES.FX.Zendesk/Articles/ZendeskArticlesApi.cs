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
}