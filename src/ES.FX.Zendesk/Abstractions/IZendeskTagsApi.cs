using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>tags</c> resource (account-wide tag usage).
/// </summary>
public interface IZendeskTagsApi
{
    /// <summary>
    ///     Lists the most popular tags of the last 60 days with usage counts
    ///     (<c>GET /api/v2/tags.json</c>; up to 20,000 tags, decreasing popularity). Subject to its own
    ///     endpoint-specific rate limit. Prefer cursor pagination (<paramref name="pageSize" /> /
    ///     <paramref name="afterCursor" />) — offset paging is capped at 10,000 records by Zendesk, leaving the
    ///     tail of the tag list unreachable.
    /// </summary>
    Task<ZendeskTagsResult> ListAsync(int? page = null, int? perPage = null, int? pageSize = null,
        string? afterCursor = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the (cached, approximate) account tag count (<c>GET /api/v2/tags/count.json</c>).</summary>
    Task<ZendeskCount> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Suggests tag names matching a prefix (<c>GET /api/v2/autocomplete/tags.json</c>; minimum two
    ///     characters, up to 15 suggestions drawn from the most-used tags of the last 60 days).
    /// </summary>
    Task<ZendeskTagNamesResult> AutocompleteAsync(string name, CancellationToken cancellationToken = default);
}