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
        "'public' flag is true for a reply visible to the requester and false for an internal agent note. " +
        "include: [\"users\"] resolves comment authors inline so you don't need per-id lookups. Read-only.")]
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
        [Description(
            "Sideloads: [\"users\"] resolves comment authors inline as a sibling 'users' array in the same call.")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetCommentsAsync(ticketId, page, perPage, bodyFormat, include: include,
                cancellationToken: cancellationToken));

    /// <summary>Returns a ticket's change history (audits/events).</summary>
    [McpServerTool(Name = "zendesk_tickets_audits", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns a ticket's change history: status/assignee/priority/field changes, comment events, and trigger/" +
        "macro/automation firings, in chronological order. Prefer include: [\"users\", \"groups\", " +
        "\"organizations\"] to resolve the actor/assignee/group ids inline as sibling arrays instead of follow-up " +
        "lookup calls; only custom-field ids still need zendesk_ticket_fields_list to decode (sideloads cannot " +
        "resolve those). For timing prefer zendesk_tickets_metrics. Read-only.")]
    public Task<ZendeskTicketAuditsResult> Audits(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        [Description("The 1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). The total is in 'count'; a non-null 'next_page' means more pages — advance 'page'.")]
        int? perPage = 25,
        [Description(
            "Sideloads resolving the ids referenced by the events inline: any of \"users\", \"groups\", \"organizations\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetAuditsAsync(ticketId, page, perPage, include: include,
                cancellationToken: cancellationToken));

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

    /// <summary>Lists tickets.</summary>
    [McpServerTool(Name = "zendesk_tickets_list", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists tickets in the account. Archived tickets are excluded (use zendesk_tickets_incremental for full " +
        "history) and the endpoint has its own per-account rate limit — prefer zendesk_tickets_search for filtered " +
        "queries. Cursor pagination: pass pageSize/afterCursor; the result's meta.has_more/meta.after_cursor drive " +
        "continuation. Sideloads resolve related records inline. Read-only.")]
    public Task<ZendeskTicketsResult> List(
        [Description("Results per page (Zendesk caps at 100; optional).")]
        int? pageSize = null,
        [Description("The continuation cursor from the previous page's meta.after_cursor (optional).")]
        string? afterCursor = null,
        [Description(
            "Sideloads to resolve ids inline as sibling arrays: any of \"users\", \"groups\", \"organizations\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.ListAsync(pageSize, afterCursor, include: include,
                cancellationToken: cancellationToken));

    /// <summary>Returns many tickets by id in one call.</summary>
    [McpServerTool(Name = "zendesk_tickets_read_many", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns many tickets by numeric id in a single call — always prefer this over repeated " +
        "zendesk_tickets_read when you have multiple ids. Zendesk accepts 100 ids per request; larger lists are " +
        "chunked and merged automatically (sideload arrays de-duplicated by id). Sideloads: \"users\", \"groups\", " +
        "\"organizations\" resolve related records as sibling arrays; \"comment_count\" populates the " +
        "comment_count field on each ticket. Read-only.")]
    public Task<ZendeskTicketsResult> ReadMany(
        [Description("The numeric Zendesk ticket ids.")]
        long[] ids,
        [Description(
            "Sideloads: \"users\", \"groups\", \"organizations\" are returned as sibling arrays; \"comment_count\" " +
            "populates the comment_count field on each ticket.")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetManyAsync(ids, include: include, cancellationToken: cancellationToken));

    /// <summary>Returns the account's total ticket count.</summary>
    [McpServerTool(Name = "zendesk_tickets_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the account's total ticket count. The value is cached/approximate (see 'refreshed_at' for its " +
        "freshness) — for the count of a filtered subset use zendesk_tickets_search and read 'count'. Read-only.")]
    public Task<ZendeskCount> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() => zendeskApiClient.Tickets.CountAsync(cancellationToken));

    /// <summary>Returns the tickets carrying an external id.</summary>
    [McpServerTool(Name = "zendesk_tickets_read_by_external_id", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the tickets carrying a given external_id — the link between Zendesk tickets and records in an " +
        "outside system (your CRM/order id, etc.). Multiple tickets can share one external id, so a list is " +
        "returned. Read-only.")]
    public Task<ZendeskTicketsResult> ReadByExternalId(
        [Description("The external id to look up (an identifier from your own system, not a Zendesk id).")]
        string externalId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetByExternalIdAsync(externalId, cancellationToken));

    /// <summary>Lists the collaborators (CCs) of a ticket.</summary>
    [McpServerTool(Name = "zendesk_tickets_collaborators", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Lists the collaborators (CCs) of a ticket as full user records — who else is copied on the conversation. " +
        "This resolves the collaborator ids on the ticket to users directly, so no zendesk_users_read follow-ups " +
        "are needed. Read-only.")]
    public Task<ZendeskUsersResult> Collaborators(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetCollaboratorsAsync(ticketId, cancellationToken));

    /// <summary>Returns a ticket's comment count.</summary>
    [McpServerTool(Name = "zendesk_tickets_comments_count", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Returns the comment count of a ticket — cheaper than paging zendesk_tickets_comments just to size the " +
        "thread. The value is cached/approximate (see 'refreshed_at' for its freshness). Read-only.")]
    public Task<ZendeskCount> CommentsCount(
        [Description("The numeric Zendesk ticket id.")]
        long ticketId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetCommentsCountAsync(ticketId, cancellationToken));

    /// <summary>Exports tickets incrementally (cursor-based incremental export).</summary>
    [McpServerTool(Name = "zendesk_tickets_incremental", ReadOnly = true, OpenWorld = true)]
    [Description(
        "Exports tickets incrementally via Zendesk's cursor-based incremental export — the recommended way to sync " +
        "full ticket history, including archived tickets that zendesk_tickets_list omits. Pass EXACTLY ONE of " +
        "startTime (Unix epoch seconds, first call only) or cursor (every subsequent call): start with startTime, " +
        "then keep passing the response's 'after_cursor' as cursor until 'end_of_stream' is true. Admin-only and " +
        "rate-limited by Zendesk's incremental export API — avoid tight polling. Sideloads (\"users\", \"groups\", " +
        "\"organizations\") resolve related records inline; \"last_audits\" is NOT supported here. Read-only.")]
    public Task<ZendeskIncrementalTicketsResult> Incremental(
        [Description(
            "Unix epoch seconds for the FIRST call. Mutually exclusive with 'cursor' — pass exactly one of the two.")]
        long? startTime = null,
        [Description(
            "The 'after_cursor' from the previous page, for every call after the first. Mutually exclusive with 'startTime'.")]
        string? cursor = null,
        [Description(
            "Sideloads to resolve inline as sibling arrays: any of \"users\", \"groups\", \"organizations\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(() =>
            zendeskApiClient.Tickets.GetIncrementalAsync(startTime, cursor, include: include,
                cancellationToken: cancellationToken));
}