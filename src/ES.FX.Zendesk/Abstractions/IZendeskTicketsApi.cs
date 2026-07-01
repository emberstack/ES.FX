using ES.FX.Zendesk.Abstractions.Models;

namespace ES.FX.Zendesk.Abstractions;

/// <summary>
///     Operations against the Zendesk <c>tickets</c> resource.
/// </summary>
public interface IZendeskTicketsApi
{
    /// <summary>Returns a ticket by id (<c>GET /api/v2/tickets/{id}.json</c>).</summary>
    Task<ZendeskTicket> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Searches tickets via the Zendesk search API (<c>GET /api/v2/search.json</c>). The query is automatically
    ///     scoped to <c>type:ticket</c> unless it already contains a <c>type:</c> selector.
    /// </summary>
    /// <param name="query">The Zendesk search query (e.g. <c>status:open priority:high</c>).</param>
    /// <param name="sortBy">
    ///     Sort field: <c>created_at</c>, <c>updated_at</c>, <c>priority</c>, <c>status</c>,
    ///     <c>ticket_type</c>.
    /// </param>
    /// <param name="sortOrder"><c>asc</c> or <c>desc</c>.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="perPage">The number of results per page (Zendesk caps at 100).</param>
    /// <param name="include">
    ///     Sideloads to resolve inline in one call (any of <c>users</c>, <c>groups</c>, <c>organizations</c>);
    ///     returned as sibling arrays on the result.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskTicketSearchResults> SearchAsync(string query, string? sortBy = null, string? sortOrder = null,
        int? page = null, int? perPage = null, IReadOnlyList<string>? include = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket's comments — the conversation thread (<c>GET /api/v2/tickets/{id}/comments.json</c>).
    /// </summary>
    /// <param name="ticketId">The ticket id.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="perPage">The number of results per page.</param>
    /// <param name="bodyFormat">
    ///     Which comment body to return: <c>plain</c> (default — plain text only, half the tokens), <c>rich</c>
    ///     (markup only), or <c>both</c>. Unrecognized values are treated as <c>plain</c>.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskTicketCommentsResult> GetCommentsAsync(long ticketId, int? page = null, int? perPage = null,
        string? bodyFormat = "plain", CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket's audits — its change history/events (<c>GET /api/v2/tickets/{id}/audits.json</c>).
    /// </summary>
    Task<ZendeskTicketAuditsResult> GetAuditsAsync(long ticketId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket's timing/lifecycle metrics (<c>GET /api/v2/tickets/{id}/metrics.json</c>).
    /// </summary>
    Task<ZendeskTicketMetric> GetMetricsAsync(long ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the incident tickets linked to a <c>problem</c> ticket
    ///     (<c>GET /api/v2/tickets/{id}/incidents.json</c>) — the blast radius of a systemic problem.
    /// </summary>
    Task<ZendeskTicketsResult> GetIncidentsAsync(long problemTicketId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a ticket's side conversations — separate vendor/escalation threads off the main comment thread
    ///     (<c>GET /api/v2/tickets/{id}/side_conversations.json</c>; requires the Collaboration add-on).
    /// </summary>
    Task<ZendeskSideConversationsResult> GetSideConversationsAsync(long ticketId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Exports ticket metric events — timestamped SLA/metric lifecycle events (<c>apply_sla</c>,
    ///     <c>breach</c>, <c>activate</c>, <c>fulfill</c>, ...) across ALL tickets, starting at
    ///     <paramref name="startTime" /> (<c>GET /api/v2/incremental/ticket_metric_events.json?start_time=...</c> —
    ///     the only metric-events endpoint Zendesk provides; there is no per-ticket variant). Filter the returned
    ///     events by <c>ticket_id</c> for a single ticket. Continue paging by passing the response's
    ///     <see cref="ZendeskMetricEventsResult.EndTime" /> as the next <paramref name="startTime" /> until
    ///     <see cref="ZendeskMetricEventsResult.EndOfStream" /> is <c>true</c>. Subject to Zendesk's incremental
    ///     export rate limits.
    /// </summary>
    /// <param name="startTime">Unix epoch seconds; events recorded at or after this time are returned.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<ZendeskMetricEventsResult> GetMetricEventsAsync(long startTime,
        CancellationToken cancellationToken = default);
}