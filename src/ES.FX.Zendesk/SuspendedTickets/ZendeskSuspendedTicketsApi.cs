using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.SuspendedTickets;

/// <summary>
///     Default <see cref="IZendeskSuspendedTicketsApi" /> implementation over the shared Zendesk
///     <see cref="HttpClient" />.
/// </summary>
internal sealed class ZendeskSuspendedTicketsApi(HttpClient httpClient, ILogger<ZendeskSuspendedTicketsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskSuspendedTicketsApi
{
    /// <inheritdoc />
    public Task<ZendeskSuspendedTicketsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build("suspended_tickets.json",
            ("page[size]", ZendeskQuery.Int(pageSize)), ("page[after]", afterCursor));
        return GetAsync<ZendeskSuspendedTicketsResult>(requestUri, "Zendesk.SuspendedTickets.List",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskSuspendedTicket> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskSuspendedTicketResponse>($"suspended_tickets/{id}.json",
            "Zendesk.SuspendedTickets.Get", cancellationToken).ConfigureAwait(false);
        return response.SuspendedTicket
               ?? throw new InvalidOperationException($"Zendesk suspended ticket '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskSuspendedTicketRecoveryResult> RecoverAsync(long id,
        CancellationToken cancellationToken = default) =>
        PutAsync<ZendeskSuspendedTicketRecoveryResult>($"suspended_tickets/{id}/recover.json",
            "Zendesk.SuspendedTickets.Recover", cancellationToken);

    /// <inheritdoc />
    public Task<ZendeskSuspendedTicketRecoveryResult> RecoverManyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        var requestUri = ZendeskQuery.Build("suspended_tickets/recover_many.json",
            ("ids", string.Join(',', ids)));
        return PutAsync<ZendeskSuspendedTicketRecoveryResult>(requestUri, "Zendesk.SuspendedTickets.RecoverMany",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"suspended_tickets/{id}.json", null, "Zendesk.SuspendedTickets.Delete",
            cancellationToken);

    /// <inheritdoc />
    public Task DeleteManyAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default)
    {
        ValidateBulkCount(ids.Count, nameof(ids));
        // QUIRK: plain 204 — this bulk delete is synchronous, unlike tickets/destroy_many.
        var requestUri = ZendeskQuery.Build("suspended_tickets/destroy_many.json",
            ("ids", string.Join(',', ids)));
        return SendAsync(HttpMethod.Delete, requestUri, null, "Zendesk.SuspendedTickets.DeleteMany",
            cancellationToken);
    }
}