using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Search;

/// <summary>
///     Default <see cref="IZendeskSearchApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskSearchApi(HttpClient httpClient, ILogger<ZendeskSearchApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskSearchApi
{
    /// <inheritdoc />
    public async Task<long> CountAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var requestUri = ZendeskQuery.Build("search/count.json", ("query", query));
        var result = await GetAsync<ZendeskSearchCountResult>(requestUri, "Zendesk.Search.Count", cancellationToken)
            .ConfigureAwait(false);
        return result.Count;
    }

    /// <inheritdoc />
    public Task<ZendeskTicketSearchExportResults> ExportTicketsAsync(string query, int? pageSize = null,
        string? afterCursor = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var requestUri = ZendeskQuery.Build("search/export.json",
            ("query", query), ("filter[type]", "ticket"),
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor));
        return GetAsync<ZendeskTicketSearchExportResults>(requestUri, "Zendesk.Search.ExportTickets",
            cancellationToken);
    }
}