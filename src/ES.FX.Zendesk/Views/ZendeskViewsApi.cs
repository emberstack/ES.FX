using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Views;

/// <summary>
///     Default <see cref="IZendeskViewsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskViewsApi(HttpClient httpClient, ILogger<ZendeskViewsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskViewsApi
{
    /// <inheritdoc />
    public Task<ZendeskViewsResult> ListAsync(bool? active = null, int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("views.json",
            ("active", ZendeskQuery.Bool(active)),
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor));
        return GetAsync<ZendeskViewsResult>(requestUri, "Zendesk.Views.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskView> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskViewResponse>($"views/{id}.json", "Zendesk.Views.Get",
            cancellationToken).ConfigureAwait(false);
        return response.View ?? throw new InvalidOperationException($"Zendesk view '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetTicketsAsync(long viewId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"views/{viewId}/tickets.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Views.Tickets", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskViewCount> GetTicketCountAsync(long viewId,
        CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskViewCountResponse>($"views/{viewId}/count.json",
            "Zendesk.Views.TicketCount", cancellationToken).ConfigureAwait(false);
        return response.ViewCount
               ?? throw new InvalidOperationException($"Zendesk returned no count for view '{viewId}'.");
    }

    /// <inheritdoc />
    public async Task<ZendeskView> CreateAsync(ZendeskViewWrite view, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskViewResponse>("views.json", new { view }, "Zendesk.Views.Create",
            cancellationToken).ConfigureAwait(false);
        return response.View ?? throw new InvalidOperationException("Zendesk returned no created view.");
    }

    /// <inheritdoc />
    public async Task<ZendeskView> UpdateAsync(long id, ZendeskViewWrite view,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskViewResponse>($"views/{id}.json", new { view },
            "Zendesk.Views.Update", cancellationToken).ConfigureAwait(false);
        return response.View ?? throw new InvalidOperationException($"Zendesk returned no view for '{id}'.");
    }

    /// <inheritdoc />
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"views/{id}.json", null, "Zendesk.Views.Delete", cancellationToken);
}