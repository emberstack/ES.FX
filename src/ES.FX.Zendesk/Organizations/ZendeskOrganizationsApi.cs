using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Organizations;

/// <summary>
///     Default <see cref="IZendeskOrganizationsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskOrganizationsApi(HttpClient httpClient, ILogger<ZendeskOrganizationsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskOrganizationsApi
{
    /// <inheritdoc />
    public async Task<ZendeskOrganization> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskOrganizationResponse>($"organizations/{id}.json",
            "Zendesk.Organizations.Get", cancellationToken).ConfigureAwait(false);
        return response.Organization
               ?? throw new InvalidOperationException($"Zendesk organization '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetTicketsAsync(long organizationId, int? page = null, int? perPage = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"organizations/{organizationId}/tickets.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)),
            ("include", ZendeskQuery.Include(include)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Organizations.Tickets", cancellationToken);
    }
}