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
    public Task<ZendeskTicketFieldsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default)
    {
        // ticket_fields documents cursor pagination or the unpaginated full list — NOT offset page/per_page.
        var requestUri = ZendeskQuery.Build("ticket_fields.json",
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor),
            ("include", ZendeskQuery.Include(include)));
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

    /// <inheritdoc />
    public Task<ZendeskCustomFieldOptionsResult> GetOptionsAsync(long ticketFieldId, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"ticket_fields/{ticketFieldId}/options.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskCustomFieldOptionsResult>(requestUri, "Zendesk.TicketFields.Options",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketField> CreateAsync(ZendeskTicketFieldWrite field,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskTicketFieldResponse>("ticket_fields.json",
            new { ticket_field = field }, "Zendesk.TicketFields.Create", cancellationToken).ConfigureAwait(false);
        return response.TicketField
               ?? throw new InvalidOperationException("Zendesk returned no created ticket field.");
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketField> UpdateAsync(long id, ZendeskTicketFieldWrite field,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskTicketFieldResponse>($"ticket_fields/{id}.json",
            new { ticket_field = field }, "Zendesk.TicketFields.Update", cancellationToken).ConfigureAwait(false);
        return response.TicketField
               ?? throw new InvalidOperationException($"Zendesk returned no ticket field for '{id}'.");
    }

    /// <inheritdoc />
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"ticket_fields/{id}.json", null, "Zendesk.TicketFields.Delete",
            cancellationToken);

    /// <inheritdoc />
    public async Task<ZendeskCustomFieldOption> CreateOrUpdateOptionAsync(long ticketFieldId,
        ZendeskCustomFieldOptionWrite option, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskCustomFieldOptionResponse>(
            $"ticket_fields/{ticketFieldId}/options.json", new { custom_field_option = option },
            "Zendesk.TicketFields.CreateOrUpdateOption", cancellationToken).ConfigureAwait(false);
        return response.CustomFieldOption
               ?? throw new InvalidOperationException("Zendesk returned no custom field option.");
    }

    /// <inheritdoc />
    public Task DeleteOptionAsync(long ticketFieldId, long optionId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"ticket_fields/{ticketFieldId}/options/{optionId}.json", null,
            "Zendesk.TicketFields.DeleteOption", cancellationToken);
}