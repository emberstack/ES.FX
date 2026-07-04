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
    public Task<ZendeskTicketFormsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("ticket_forms.json",
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor));
        return GetAsync<ZendeskTicketFormsResult>(requestUri, "Zendesk.Forms.List", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketForm> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskTicketFormResponse>($"ticket_forms/{id}.json", "Zendesk.Forms.Get",
            cancellationToken).ConfigureAwait(false);
        return response.TicketForm ?? throw new InvalidOperationException($"Zendesk ticket form '{id}' was not found.");
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketForm> CreateAsync(ZendeskTicketFormWrite form,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<ZendeskTicketFormResponse>("ticket_forms.json", new { ticket_form = form },
            "Zendesk.Forms.Create", cancellationToken).ConfigureAwait(false);
        return response.TicketForm ?? throw new InvalidOperationException("Zendesk returned no created form.");
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketForm> UpdateAsync(long id, ZendeskTicketFormWrite form,
        CancellationToken cancellationToken = default)
    {
        var response = await PutAsync<ZendeskTicketFormResponse>($"ticket_forms/{id}.json",
            new { ticket_form = form }, "Zendesk.Forms.Update", cancellationToken).ConfigureAwait(false);
        return response.TicketForm ?? throw new InvalidOperationException($"Zendesk returned no form for '{id}'.");
    }

    /// <inheritdoc />
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"ticket_forms/{id}.json", null, "Zendesk.Forms.Delete", cancellationToken);

    /// <inheritdoc />
    public async Task<ZendeskTicketForm> CloneAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<ZendeskTicketFormResponse>(HttpMethod.Post,
            $"ticket_forms/{id}/clone.json", null, "Zendesk.Forms.Clone", cancellationToken).ConfigureAwait(false);
        return response.TicketForm ?? throw new InvalidOperationException("Zendesk returned no cloned form.");
    }
}