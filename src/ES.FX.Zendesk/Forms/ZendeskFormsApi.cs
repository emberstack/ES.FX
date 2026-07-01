using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Forms;

/// <summary>
///     Default <see cref="IZendeskFormsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskFormsApi(HttpClient httpClient, ILogger<ZendeskFormsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskFormsApi
{
    /// <inheritdoc />
    public Task<ZendeskTicketFormsResult> ListAsync(CancellationToken cancellationToken = default)
        => GetAsync<ZendeskTicketFormsResult>("ticket_forms.json", "Zendesk.Forms.List", cancellationToken);

    /// <inheritdoc />
    public async Task<ZendeskTicketForm> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskTicketFormResponse>($"ticket_forms/{id}.json", "Zendesk.Forms.Get",
            cancellationToken).ConfigureAwait(false);
        return response.TicketForm ?? throw new InvalidOperationException($"Zendesk ticket form '{id}' was not found.");
    }
}