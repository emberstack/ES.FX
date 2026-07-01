using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>ticket_forms</c> resource.
/// </summary>
public interface IZendeskFormsApi
{
    /// <summary>
    ///     Lists the ticket forms (<c>GET /api/v2/ticket_forms.json</c>).
    /// </summary>
    Task<ZendeskTicketFormsResult> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket form by id (<c>GET /api/v2/ticket_forms/{id}.json</c>).
    /// </summary>
    Task<ZendeskTicketForm> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}