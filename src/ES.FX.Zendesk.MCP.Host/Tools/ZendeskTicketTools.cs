using System.ComponentModel;
using ES.FX.Zendesk.Abstractions;
using ES.FX.Zendesk.Abstractions.Models;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk tickets. Namespaced <c>zendesk_tickets_*</c> to mirror the Zendesk API structure.
/// </summary>
[McpServerToolType]
public sealed class ZendeskTicketTools(IZendeskClient zendeskApiClient)
{
    /// <summary>Returns a Zendesk ticket by id.</summary>
    [McpServerTool(Name = "zendesk_tickets_read", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a single Zendesk ticket by numeric id, including requester/assignee/group/organization ids, tags, " +
        "collaborators/CCs, custom field values, satisfaction rating, and problem/incident links. Resolve the people " +
        "ids with zendesk_users_read(_many) and the group id with zendesk_groups_read; decode custom_fields with " +
        "zendesk_ticket_fields_list. Read-only.")]
    public Task<ZendeskTicket> Read(
        [Description("The numeric Zendesk ticket id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Tickets.GetByIdAsync(id, cancellationToken));

    /// <summary>Searches Zendesk tickets.</summary>
    [McpServerTool(Name = "zendesk_tickets_search", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Searches Zendesk tickets using the Zendesk search query syntax; the query is automatically scoped to " +
        "tickets. Examples: \"status:open priority:high\", \"requester:jane@example.com\", \"created>2026-01-01\", " +
        "\"tags:vip\". Returns a page of tickets plus the total count. Read-only.")]
    public Task<ZendeskTicketSearchResults> Search(
        [Description("The Zendesk search query (type:ticket is added automatically).")]
        string query,
        [Description("Sort field: created_at, updated_at, priority, status, or ticket_type (optional).")]
        string? sortBy = null,
        [Description("Sort order: asc or desc (optional).")]
        string? sortOrder = null,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Use 'page' to fetch more; the total match count is in 'count'.")]
        int? perPage = 25,
        [Description(
            "Sideloads to resolve ids inline in one call: any of \"users\", \"groups\", \"organizations\". Returned as sibling arrays so you don't need per-id lookups.")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.SearchAsync(query, sortBy, sortOrder, page, perPage, include, cancellationToken));

    /// <summary>Returns the conversation thread (comments) on a ticket.</summary>
    [McpServerTool(Name = "zendesk_tickets_comments", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the conversation thread (comments) on a ticket — the actual back-and-forth. Each comment's " +
        "'public' flag is true for a reply visible to the requester and false for an internal agent note. Read-only.")]
    public Task<ZendeskTicketCommentsResult> Comments(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Use 'page' for the full thread; the total is in 'count'.")]
        int? perPage = 25,
        [Description(
            "Body to return: \"plain\" (default, plain text — half the tokens), \"rich\" (markup), or \"both\".")]
        string? bodyFormat = "plain",
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetCommentsAsync(ticketId, page, perPage, bodyFormat, cancellationToken));

    /// <summary>Returns a ticket's change history (audits/events).</summary>
    [McpServerTool(Name = "zendesk_tickets_audits", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a ticket's change history: status/assignee/priority/field changes, comment events, and trigger/" +
        "macro/automation firings, in chronological order. Field-change events carry raw ids (assignee_id, group_id, " +
        "custom field ids) — resolve them with the users/groups/ticket_fields tools; for timing prefer " +
        "zendesk_tickets_metrics. Read-only.")]
    public Task<ZendeskTicketAuditsResult> Audits(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). The total is in 'count'; a non-null 'next_page' means more pages — advance 'page'.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetAuditsAsync(ticketId, page, perPage, cancellationToken));

    /// <summary>Returns timing/lifecycle metrics for a ticket.</summary>
    [McpServerTool(Name = "zendesk_tickets_metrics", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns timing and lifecycle metrics for a ticket: first-reply and resolution times, number of reopens " +
        "(a frustration signal), reply count, and wait times. Use to gauge urgency. Read-only.")]
    public Task<ZendeskTicketMetric> Metrics(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Tickets.GetMetricsAsync(ticketId, cancellationToken));

    /// <summary>Returns the incidents linked to a problem ticket.</summary>
    [McpServerTool(Name = "zendesk_tickets_incidents", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the incident tickets linked to a problem ticket — the blast radius of a systemic issue (how many " +
        "customers are hit by the same root cause). Only meaningful for a ticket of type 'problem' (see a ticket's " +
        "'has_incidents' flag). Read-only.")]
    public Task<ZendeskTicketsResult> Incidents(
        [Description("The numeric id of the problem ticket.")]
        long problemTicketId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). The total is in 'count'; a non-null 'next_page' means more pages — advance 'page'.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetIncidentsAsync(problemTicketId, page, perPage, cancellationToken));

    /// <summary>Returns a ticket's side conversations (vendor/escalation threads).</summary>
    [McpServerTool(Name = "zendesk_tickets_side_conversations", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a ticket's side conversations — separate email/Slack/child-ticket threads agents use to loop in a " +
        "vendor or another team. These are NOT in the main comment thread, so check here before concluding nothing " +
        "happened on an escalated ticket. Requires the Collaboration add-on (errors cleanly if unavailable). Read-only.")]
    public Task<ZendeskSideConversationsResult> SideConversations(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description("Results per page (optional). The total is in 'count'; a non-null 'next_page' means more pages.")]
        int? perPage = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetSideConversationsAsync(ticketId, page, perPage, cancellationToken));

    /// <summary>Exports SLA/metric events across tickets (breach timeline).</summary>
    [McpServerTool(Name = "zendesk_tickets_metric_events", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Exports ticket metric events — the timestamped SLA/metric lifecycle stream (apply_sla, breach, activate, " +
        "fulfill, ...) for ALL tickets with events at or after startTime (Unix epoch seconds). Zendesk has no " +
        "per-ticket variant, so filter the events by ticket_id to analyze one ticket. Unlike zendesk_tickets_metrics " +
        "(aggregate durations), this shows WHEN an SLA target was applied or breached. Page by passing the response's " +
        "'end_time' as the next startTime until 'end_of_stream' is true. Rate-limited by Zendesk's incremental export " +
        "API — avoid tight polling. Read-only.")]
    public Task<ZendeskMetricEventsResult> MetricEvents(
        [Description("Unix epoch seconds; metric events recorded at or after this time are returned.")]
        long startTime,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetMetricEventsAsync(startTime, cancellationToken));
}