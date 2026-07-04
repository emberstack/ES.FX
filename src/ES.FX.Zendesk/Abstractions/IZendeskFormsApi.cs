using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>ticket_forms</c> resource.
/// </summary>
public interface IZendeskFormsApi
{
    /// <summary>
    ///     Lists the ticket forms (<c>GET /api/v2/ticket_forms.json</c>; cursor-paginated — page with
    ///     <c>Meta.AfterCursor</c> when an account has more than one page of forms).
    /// </summary>
    Task<ZendeskTicketFormsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket form by id (<c>GET /api/v2/ticket_forms/{id}.json</c>).
    /// </summary>
    Task<ZendeskTicketForm> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Creates a ticket form (<c>POST /api/v2/ticket_forms.json</c>; admin-only, multi-form plans).</summary>
    Task<ZendeskTicketForm> CreateAsync(ZendeskTicketFormWrite form, CancellationToken cancellationToken = default);

    /// <summary>Updates a ticket form (<c>PUT /api/v2/ticket_forms/{id}.json</c>; admin-only).</summary>
    Task<ZendeskTicketForm> UpdateAsync(long id, ZendeskTicketFormWrite form,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a ticket form (<c>DELETE /api/v2/ticket_forms/{id}.json</c>; the default form cannot be deleted).</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Clones a ticket form (<c>POST /api/v2/ticket_forms/{id}/clone.json</c>) and returns the copy.</summary>
    Task<ZendeskTicketForm> CloneAsync(long id, CancellationToken cancellationToken = default);
}