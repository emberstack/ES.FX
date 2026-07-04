using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Tags;

/// <summary>
///     Default <see cref="IZendeskTagsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskTagsApi(HttpClient httpClient, ILogger<ZendeskTagsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskTagsApi
{
    /// <inheritdoc />
    public Task<ZendeskTagsResult> ListAsync(int? page = null, int? perPage = null, int? pageSize = null,
        string? afterCursor = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("tags.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor));
        return GetAsync<ZendeskTagsResult>(requestUri, "Zendesk.Tags.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskCount> CountAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskCountResponse>("tags/count.json", "Zendesk.Tags.Count",
            cancellationToken).ConfigureAwait(false);
        return response.Count ?? throw new InvalidOperationException("Zendesk returned no tag count.");
    }

    /// <inheritdoc />
    public Task<ZendeskTagNamesResult> AutocompleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var requestUri = ZendeskQuery.Build("autocomplete/tags.json", ("name", name));
        return GetAsync<ZendeskTagNamesResult>(requestUri, "Zendesk.Tags.Autocomplete", cancellationToken);
    }
}