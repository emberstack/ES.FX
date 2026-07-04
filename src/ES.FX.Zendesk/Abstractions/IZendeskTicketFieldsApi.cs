using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>ticket_fields</c> definitions (used to interpret custom field values on
///     tickets).
/// </summary>
public interface IZendeskTicketFieldsApi
{
    /// <summary>
    ///     Lists ticket field definitions (<c>GET /api/v2/ticket_fields.json</c>). Without pagination
    ///     parameters, Zendesk returns ALL fields in position order; with <paramref name="pageSize" /> /
    ///     <paramref name="afterCursor" /> it cursor-paginates (max 100/page, ordered by id) — this endpoint
    ///     does not document offset paging. <paramref name="include" /> sideloads (<c>users</c>) resolve the
    ///     field creators inline.
    /// </summary>
    Task<ZendeskTicketFieldsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        IReadOnlyList<string>? include = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a ticket field definition by id (<c>GET /api/v2/ticket_fields/{id}.json</c>).</summary>
    Task<ZendeskTicketField> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists the custom options of a drop-down ticket field
    ///     (<c>GET /api/v2/ticket_fields/{id}/options.json</c>; <c>custom_field_options</c> envelope).
    /// </summary>
    Task<ZendeskCustomFieldOptionsResult> GetOptionsAsync(long ticketFieldId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a ticket field (<c>POST /api/v2/ticket_fields.json</c>; admin-only). <c>Type</c> is immutable
    ///     afterwards.
    /// </summary>
    Task<ZendeskTicketField> CreateAsync(ZendeskTicketFieldWrite field,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates a ticket field (<c>PUT /api/v2/ticket_fields/{id}.json</c>; admin-only). WARNING: sending
    ///     <c>CustomFieldOptions</c> replaces the whole option set — omitted options are DELETED and their values
    ///     removed from tickets.
    /// </summary>
    Task<ZendeskTicketField> UpdateAsync(long id, ZendeskTicketFieldWrite field,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a ticket field (<c>DELETE /api/v2/ticket_fields/{id}.json</c>; admin-only).</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a custom field option, or updates one when <c>Id</c> is set
    ///     (<c>POST /api/v2/ticket_fields/{id}/options.json</c>; upsert semantics, rate-limited to 100/min).
    /// </summary>
    Task<ZendeskCustomFieldOption> CreateOrUpdateOptionAsync(long ticketFieldId,
        ZendeskCustomFieldOptionWrite option, CancellationToken cancellationToken = default);

    /// <summary>Deletes a custom field option (<c>DELETE /api/v2/ticket_fields/{id}/options/{optionId}.json</c>).</summary>
    Task DeleteOptionAsync(long ticketFieldId, long optionId, CancellationToken cancellationToken = default);
}