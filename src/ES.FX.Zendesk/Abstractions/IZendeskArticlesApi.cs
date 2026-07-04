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

    /// <summary>
    ///     Lists Help Center articles (<c>GET /api/v2/help_center/[{locale}/]articles.json</c>;
    ///     <c>articles</c> envelope, cursor-paginated). Optionally scoped to a section.
    /// </summary>
    /// <param name="locale">Optional locale segment (e.g. <c>en-us</c>); agents may omit it.</param>
    /// <param name="sectionId">When set, lists the articles of that section only.</param>
    /// <param name="pageSize">The cursor page size (max 100).</param>
    /// <param name="afterCursor">The cursor from the previous page's <c>Meta.AfterCursor</c>.</param>
    /// <param name="include">
    ///     Sideloads (<c>users</c>, <c>sections</c>, <c>categories</c>) resolved inline as sibling arrays.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskArticlesResult> ListAsync(string? locale = null, long? sectionId = null, int? pageSize = null,
        string? afterCursor = null, IReadOnlyList<string>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists Help Center sections (<c>GET /api/v2/help_center/[{locale}/]sections.json</c>), optionally
    ///     scoped to a category.
    /// </summary>
    Task<ZendeskHelpCenterSectionsResult> ListSectionsAsync(string? locale = null, long? categoryId = null,
        int? page = null, int? perPage = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a Help Center section by id (<c>GET /api/v2/help_center/[{locale}/]sections/{id}.json</c>).</summary>
    Task<ZendeskHelpCenterSection> GetSectionByIdAsync(long id, string? locale = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lists Help Center categories (<c>GET /api/v2/help_center/[{locale}/]categories.json</c>).</summary>
    Task<ZendeskHelpCenterCategoriesResult> ListCategoriesAsync(string? locale = null, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a Help Center category by id (<c>GET /api/v2/help_center/[{locale}/]categories/{id}.json</c>).</summary>
    Task<ZendeskHelpCenterCategory> GetCategoryByIdAsync(long id, string? locale = null,
        CancellationToken cancellationToken = default);
}