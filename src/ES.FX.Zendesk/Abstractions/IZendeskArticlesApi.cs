using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk Help Center (Guide) <c>articles</c> knowledge base.
/// </summary>
public interface IZendeskArticlesApi
{
    /// <summary>
    ///     Full-text searches Help Center articles (<c>GET /api/v2/help_center/articles/search</c>).
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="locale">Optional locale to scope the search (e.g. <c>en-us</c>).</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="perPage">The number of results per page (max 100).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskArticleSearchResults> SearchAsync(string query, string? locale = null, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a single article, including its full body (<c>GET /api/v2/help_center/articles/{id}</c>).</summary>
    Task<ZendeskArticle> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}