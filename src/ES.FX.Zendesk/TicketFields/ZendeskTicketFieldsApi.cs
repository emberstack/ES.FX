using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.TicketFields;

/// <summary>
///     Default <see cref="IZendeskTicketFieldsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskTicketFieldsApi(HttpClient httpClient, ILogger<ZendeskTicketFieldsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskTicketFieldsApi
{
    /// <inheritdoc />
    public Task<ZendeskTicketFieldsResult> ListAsync(int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("ticket_fields.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskTicketFieldsResult>(requestUri, "Zendesk.TicketFields.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketField> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskTicketFieldResponse>($"ticket_fields/{id}.json",
            "Zendesk.TicketFields.Get",
            cancellationToken).ConfigureAwait(false);
        return response.TicketField ??
               throw new InvalidOperationException($"Zendesk ticket field '{id}' was not found.");
    }
}