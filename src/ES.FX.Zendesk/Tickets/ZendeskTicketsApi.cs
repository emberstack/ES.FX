using System.Globalization;
using System.Text.RegularExpressions;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace ES.FX.Zendesk.Tickets;

/// <summary>
///     Default <see cref="IZendeskTicketsApi" /> implementation over the shared Zendesk <see cref="HttpClient" />.
/// </summary>
internal sealed partial class ZendeskTicketsApi(HttpClient httpClient, ILogger<ZendeskTicketsApi> logger)
    : ZendeskResourceApi(httpClient, logger), IZendeskTicketsApi
{
    /// <inheritdoc />
    public async Task<ZendeskTicket> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskTicketResponse>($"tickets/{id}.json", "Zendesk.Tickets.Get",
            cancellationToken).ConfigureAwait(false);
        return response.Ticket ?? throw new InvalidOperationException($"Zendesk ticket '{id}' was not found.");
    }

    /// <inheritdoc />
    public Task<ZendeskTicketSearchResults> SearchAsync(string query, string? sortBy = null, string? sortOrder = null,
        int? page = null, int? perPage = null, IReadOnlyList<string>? include = null,
        CancellationToken cancellationToken = default)
    {
        // Scope the search to tickets unless the caller already supplied a `type:` result-type selector.
        // Match `type:` only at a token boundary so unrelated qualifiers (ticket_type:, support_type:,
        // content-type:, or free text) do not disable scoping and leave the search running across all record types.
        var scopedQuery = TypeSelectorRegex().IsMatch(query) ? query : $"type:ticket {query}".Trim();

        // The Search API sideloads with the nested `include=tickets(users,organizations)` syntax (unlike list
        // endpoints, which use a flat list).
        var sideload = ZendeskQuery.Include(include);
        var scopedInclude = sideload is null ? null : $"tickets({sideload})";

        var requestUri = ZendeskQuery.Build("search.json",
            ("query", scopedQuery), ("sort_by", sortBy), ("sort_order", sortOrder),
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)), ("include", scopedInclude));
        return GetAsync<ZendeskTicketSearchResults>(requestUri, "Zendesk.Tickets.Search", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketCommentsResult> GetCommentsAsync(long ticketId, int? page = null,
        int? perPage = null, string? bodyFormat = "plain", CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"tickets/{ticketId}/comments.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        var result = await GetAsync<ZendeskTicketCommentsResult>(requestUri, "Zendesk.Tickets.Comments",
            cancellationToken).ConfigureAwait(false);
        return ProjectBodies(result, bodyFormat);
    }

    /// <inheritdoc />
    public Task<ZendeskTicketAuditsResult> GetAuditsAsync(long ticketId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"tickets/{ticketId}/audits.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskTicketAuditsResult>(requestUri, "Zendesk.Tickets.Audits", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ZendeskTicketMetric> GetMetricsAsync(long ticketId, CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<ZendeskTicketMetricResponse>($"tickets/{ticketId}/metrics.json",
            "Zendesk.Tickets.Metrics", cancellationToken).ConfigureAwait(false);
        return response.TicketMetric
               ?? throw new InvalidOperationException($"Zendesk returned no metrics for ticket '{ticketId}'.");
    }

    /// <inheritdoc />
    public Task<ZendeskTicketsResult> GetIncidentsAsync(long problemTicketId, int? page = null, int? perPage = null,
        CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"tickets/{problemTicketId}/incidents.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskTicketsResult>(requestUri, "Zendesk.Tickets.Incidents", cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskSideConversationsResult> GetSideConversationsAsync(long ticketId, int? page = null,
        int? perPage = null, CancellationToken cancellationToken = default)
    {
        var requestUri = ZendeskQuery.Build($"tickets/{ticketId}/side_conversations.json",
            ("page", ZendeskQuery.Int(page)), ("per_page", ZendeskQuery.Int(perPage)));
        return GetAsync<ZendeskSideConversationsResult>(requestUri, "Zendesk.Tickets.SideConversations",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<ZendeskMetricEventsResult> GetMetricEventsAsync(long startTime,
        CancellationToken cancellationToken = default)
    {
        // The incremental export is the ONLY metric-events endpoint Zendesk provides. A per-ticket
        // tickets/{id}/metric_events path does not exist (live-verified: it answers 200 with an empty body).
        var requestUri = ZendeskQuery.Build("incremental/ticket_metric_events.json",
            ("start_time", startTime.ToString(CultureInfo.InvariantCulture)));
        return GetAsync<ZendeskMetricEventsResult>(requestUri, "Zendesk.Tickets.MetricEvents", cancellationToken);
    }

    // Comments carry both a plain and a rich body; returning both doubles the tokens for a long thread. Trim to
    // the requested format (plain by default) so the caller pays for one representation unless it asks for both.
    private static ZendeskTicketCommentsResult ProjectBodies(ZendeskTicketCommentsResult result, string? bodyFormat)
    {
        var format = (bodyFormat ?? "plain").Trim().ToLowerInvariant();
        if (format == "both") return result;

        var comments = result.Comments
            .Select(comment => format == "rich"
                ? comment with { PlainBody = null }
                : comment with { Body = null })
            .ToList();
        return result with { Comments = comments };
    }

    [GeneratedRegex(@"(^|\s)type:", RegexOptions.IgnoreCase)]
    private static partial Regex TypeSelectorRegex();
}