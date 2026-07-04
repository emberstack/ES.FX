using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>suspended_tickets</c> resource — inbound messages held out of the ticket
///     stream. Ids are suspended-ticket ids, NOT ticket ids.
/// </summary>
public interface IZendeskSuspendedTicketsApi
{
    /// <summary>Lists suspended tickets (<c>GET /api/v2/suspended_tickets.json</c>; cursor-paginated).</summary>
    Task<ZendeskSuspendedTicketsResult> ListAsync(int? pageSize = null, string? afterCursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a suspended ticket by id (<c>GET /api/v2/suspended_tickets/{id}.json</c>).</summary>
    Task<ZendeskSuspendedTicket> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Recovers a suspended ticket into a real ticket (<c>PUT /api/v2/suspended_tickets/{id}/recover.json</c>).
    ///     SIDE EFFECT: the requester becomes the calling agent — use <see cref="RecoverManyAsync" /> with a
    ///     single id to preserve the original requester.
    /// </summary>
    Task<ZendeskSuspendedTicketRecoveryResult> RecoverAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Recovers up to 100 suspended tickets (<c>PUT /api/v2/suspended_tickets/recover_many.json</c>;
    ///     synchronous, preserves original requesters).
    /// </summary>
    Task<ZendeskSuspendedTicketRecoveryResult> RecoverManyAsync(IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a suspended ticket (<c>DELETE /api/v2/suspended_tickets/{id}.json</c>).</summary>
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes up to 100 suspended tickets (<c>DELETE /api/v2/suspended_tickets/destroy_many.json</c>;
    ///     synchronous <c>204</c>, NOT an async job).
    /// </summary>
    Task DeleteManyAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default);
}