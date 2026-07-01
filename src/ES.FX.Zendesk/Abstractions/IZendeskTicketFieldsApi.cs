using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>ticket_fields</c> definitions (used to interpret custom field values on
///     tickets).
/// </summary>
public interface IZendeskTicketFieldsApi
{
    /// <summary>Lists ticket field definitions (<c>GET /api/v2/ticket_fields.json</c>).</summary>
    Task<ZendeskTicketFieldsResult> ListAsync(int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a ticket field definition by id (<c>GET /api/v2/ticket_fields/{id}.json</c>).</summary>
    Task<ZendeskTicketField> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}