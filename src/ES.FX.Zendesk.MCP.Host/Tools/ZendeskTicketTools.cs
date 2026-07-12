using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.Support;
using ES.FX.Zendesk.Support.Api.V2.Tickets.Item.Comments;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk tickets. Namespaced <c>tickets_*</c> to mirror the Zendesk API structure.
/// </summary>
/// <remarks>
///     Requests are built from the generated request builders but sent through
///     <see cref="ZendeskKiotaRequests.SendForJsonAsync" /> (raw JSON passthrough) instead of the typed models:
///     the generated ticket models mark the fields that matter here as read-only and drop them on
///     re-serialization (<c>TicketObject</c> loses <c>id</c>/<c>created_at</c>/<c>updated_at</c>/<c>tags</c>,
///     and the list envelopes lose <c>count</c>/<c>next_page</c>/<c>meta</c> plus the sideload arrays their tool
///     descriptions promise). The escape hatches also supply the query parameters and endpoints the published
///     spec omits (cursor pagination on <c>tickets</c>, <c>per_page</c> on audits/incidents/the incremental
///     export, and the side-conversations endpoint, which is absent from the spec entirely — see the
///     spec-anomaly ledger in <c>src/ES.FX.Zendesk/OpenApi/README.md</c>). List/search responses are then
///     projected through <see cref="ZendeskLean" /> into the uniform lean envelope — summary rows by default,
///     complete records via <c>detail:'full'</c> or <c>tickets_get</c>.
/// </remarks>
[McpServerToolType]
public sealed partial class ZendeskTicketTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c>; larger lists are rejected with 400.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <summary>
    ///     Zendesk's server-side default page size, used to compute a comment's absolute index for the
    ///     truncation-marker re-call when the agent explicitly nulled <c>perPage</c>.
    /// </summary>
    private const int ZendeskServerDefaultPerPage = 100;

    /// <summary>The uniform <c>detail</c> parameter description shared by the list/search tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records, null fields and API links omitted).";

    /// <summary>The well-known values of the comments <c>format</c> parameter (a client-side projection).</summary>
    private const string CommentsFormatJson = "json";

    private const string CommentsFormatMarkdown = "markdown";

    /// <summary>
    ///     Serializer for the projected comments page re-emitted as wire JSON for the envelope builder: the
    ///     <see cref="JsonPropertyName" /> mappings restore the Zendesk shape and <c>WhenWritingNull</c> drops the
    ///     per-comment fields the projection nulled (the discarded body representation, pruned attachments) so
    ///     they do not repeat as JSON nulls.
    /// </summary>
    private static readonly JsonSerializerOptions CommentsProjectionJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Returns a Zendesk ticket by id.</summary>
    [McpServerTool(Name = "tickets_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single ticket by numeric id — full-detail record (null fields and API self-links omitted; absent field " +
        "= null/empty): requester/assignee/group/organization ids, tags, collaborators/CCs, custom field values, " +
        "satisfaction rating, problem/incident links. 'description' is the FIRST comment only — full thread via " +
        "tickets_comments_list. Resolve people ids with users_get(_many), group id with groups_get; decode " +
        "custom_fields with ticket_fields_list.")]
    public Task<JsonElement> Read(
        [Description("Numeric Zendesk ticket id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Tickets[id].ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("ticket", out var ticket)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(ticket), "tickets_get",
                    MaxResponseChars("tickets_get"),
                    "Ticket record exceeds the response budget — fetch its parts via tickets_comments_list, " +
                    "tickets_audits_list, or tickets_metrics_get.")
                : throw new McpException($"Zendesk ticket '{id}' was not found.");
        });

    /// <summary>Returns the conversation thread (comments) on a ticket.</summary>
    [McpServerTool(Name = "tickets_comments_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Conversation thread (comments) on a ticket, as the uniform list envelope (items[], numeric next_page, " +
        "has_more, note). Per-comment 'public': true = reply visible to requester, false = internal agent note. " +
        "Default perPage 10 (max 100); thread total in 'count' (tickets_comments_count is cheaper for sizing). " +
        "Bodies capped at maxBodyChars (default 2000) per comment; a truncation marker names the exact re-call for " +
        "full text. Triage a long thread: preflight tickets_comments_count, then read order:'newest' with small " +
        "perPage. include:[\"users\"] resolves comment authors inline. format:'markdown' returns a compact " +
        "human-readable transcript instead of json rows (drops the ids needed for follow-up chaining).")]
    public Task<JsonElement> Comments(
        [Description("Numeric Zendesk ticket id.")]
        long ticketId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description("Results per page (default 10, max 100). Walk the thread via 'page'; total in 'count'.")]
        int? perPage = 10,
        [Description(
            "Body to return: \"plain\" (default, ~half the tokens) | \"rich\" (markup) | \"both\".")]
        string? bodyFormat = "plain",
        [Description(
            "Sideloads: [\"users\"] resolves comment authors inline as a sibling 'users' array.")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(
            "Per-comment body cap in chars (default 2000; 0 = no limit). A capped body ends with a marker naming " +
            "the exact re-call (maxBodyChars:0, perPage:1, page:<n>) returning that comment in full.")]
        int maxBodyChars = 2000,
        [Description(
            "Thread order: 'oldest' (default, chronological) | 'newest' (most recent first — cheap way to triage a " +
            "long thread).")]
        string order = "oldest",
        [Description(
            "Output shape: 'json' (default, structured rows carrying the ids that chain into users_get / " +
            "tickets_get) | 'markdown' (a compact author/date/visibility/body transcript for reading, not chaining).")]
        string format = "json")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var bodyFormatValue = ParseBodyFormat(bodyFormat);
            var newestFirst = ParseOrder(order);
            var markdown = ParseCommentsFormat(format);
            if (maxBodyChars < 0)
                throw new McpException(
                    $"Invalid maxBodyChars value '{maxBodyChars}'. Pass a positive character cap, or 0 for no limit.");

            var request = zendesk.Api.V2.Tickets[ticketId].Comments.ToGetRequestInformation(configuration =>
            {
                configuration.QueryParameters.Include = Sideloads(include);
                configuration.QueryParameters.Page = page?.ToString(CultureInfo.InvariantCulture);
                configuration.QueryParameters.PerPage = perPage;
                // 'oldest' is the API default (asc) — only the opt-in emits a sort_order on the wire.
                if (newestFirst) configuration.QueryParameters.SortOrder = GetSort_orderQueryParameterType.Desc;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            if (json.ValueKind != JsonValueKind.Object)
                throw new McpException("The Zendesk API returned an empty response where a payload was expected.");
            var result = json.Deserialize<ZendeskTicketCommentsResult>() ?? new ZendeskTicketCommentsResult();

            // Pre-pass: body selection, per-comment maxBodyChars cap and empty-attachment pruning. The envelope
            // builder then applies the uniform items/has_more/next_page/note contract and the page-total size guard.
            var projected = ProjectComments(result, bodyFormatValue, maxBodyChars, newestFirst, page, perPage);
            if (markdown)
                return RenderCommentsMarkdown(projected, page);

            // WhenWritingNull so per-comment fields nulled by the projection (the discarded body representation,
            // pruned attachments) do not repeat as JSON nulls; detail:'full' feeds the already-lean rows through
            // ToFullView (there is no comment summary shape) so the envelope preserves them verbatim.
            var wire = JsonSerializer.SerializeToElement(projected, CommentsProjectionJsonOptions);
            return ZendeskLean.BuildOffsetListEnvelope(wire, "comments", page, ZendeskDetail.Full,
                MaxResponseChars("tickets_comments_list"));
        });

    /// <summary>Returns a ticket's change history (audits/events).</summary>
    [McpServerTool(Name = "tickets_audits_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Ticket change history as lean audit rows, chronological: author/when, trigger/macro/automation " +
        "attribution (via.source), per-event changes (field_name, previous_value, value). Comment events carry a " +
        "200-char excerpt; voice comments only their identity — full thread in tickets_comments_list. Default " +
        "perPage 10; detail:'full' for complete audits (full event payloads and metadata). Prefer " +
        "include:[\"users\",\"groups\",\"organizations\"] to resolve actor/assignee/group ids inline as sibling " +
        "arrays; only custom-field ids still need ticket_fields_list to decode (sideloads cannot resolve those). " +
        "For timing prefer tickets_metrics_get.")]
    public Task<JsonElement> Audits(
        [Description("Numeric Zendesk ticket id.")]
        long ticketId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 10, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 10,
        [Description(
            "Sideloads resolving event-referenced ids inline: any of \"users\", \"groups\", \"organizations\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The generated audits builder exposes include/page but not per_page — supply it via the escape hatch.
            // The include values (users, groups, organizations) are doc-verified, not spec-enumerated:
            // https://developer.zendesk.com/documentation/ticketing/using-the-zendesk-api/side_loading/
            // (ticket audits also support "tickets", deliberately not offered here).
            var request = zendesk.Api.V2.Tickets[ticketId].Audits.ToGetRequestInformation(configuration =>
            {
                configuration.QueryParameters.Include = Sideloads(include);
                configuration.QueryParameters.Page = page;
            }).WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "audits", page, parsedDetail,
                MaxResponseChars("tickets_audits_list"));
        });

    /// <summary>Returns timing/lifecycle metrics for a ticket.</summary>
    [McpServerTool(Name = "tickets_metrics_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Timing/lifecycle metrics for a ticket: first-reply and resolution times, reopen count (frustration " +
        "signal), reply count, wait times. Gauges urgency. Null-valued metrics and API links omitted — absent " +
        "field = milestone not reached yet.")]
    public Task<JsonElement> Metrics(
        [Description("Numeric Zendesk ticket id.")]
        long ticketId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            // The spec mis-models this response ('ticket_metric' as an array); the live API returns a single
            // object — unwrap it from the raw JSON instead of trusting the generated model.
            var request = zendesk.Api.V2.Tickets[ticketId].Metrics.ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("ticket_metric", out var metric)
                ? ZendeskLean.EnsureWithinBudget(ZendeskLean.ToFullView(metric), "tickets_metrics_get",
                    MaxResponseChars("tickets_metrics_get"),
                    "Ticket record exceeds the response budget — fetch its parts via tickets_comments_list, " +
                    "tickets_audits_list, or tickets_metrics_get.")
                : throw new McpException($"Zendesk returned no metrics for ticket '{ticketId}'.");
        });

    /// <summary>Returns the incidents linked to a problem ticket.</summary>
    [McpServerTool(Name = "tickets_incidents_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Incident tickets linked to a problem ticket — the blast radius of a systemic issue (customers hit by the " +
        "same root cause). Only meaningful for type 'problem' (see 'has_incidents' flag). For just the count, call " +
        "perPage:1 and read 'count' — don't page rows. Rows are lean ticket summaries (default perPage 25); " +
        "detail:'full' for complete records, or tickets_get for one.")]
    public Task<JsonElement> Incidents(
        [Description("Numeric id of the problem ticket.")]
        long problemTicketId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The generated incidents builder exposes no paging parameters — supply them via the escape hatch.
            var request = zendesk.Api.V2.Tickets[problemTicketId].Incidents.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "tickets", page, parsedDetail,
                MaxResponseChars("tickets_incidents_list"));
        });

    /// <summary>Returns a ticket's side conversations (vendor/escalation threads).</summary>
    [McpServerTool(Name = "tickets_side_conversations_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "A ticket's side conversations — separate email/Slack/child-ticket threads agents use to loop in a vendor " +
        "or another team. NOT in the main comment thread, so check here before concluding nothing happened on an " +
        "escalated ticket. Rows are lean summaries (subject, state, participants, 200-char preview; default " +
        "perPage 25) — detail:'full' for complete records. Requires the Collaboration add-on (errors cleanly if " +
        "unavailable).")]
    public Task<JsonElement> SideConversations(
        [Description("Numeric Zendesk ticket id.")]
        long ticketId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 25). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // The side-conversations endpoint is absent from the published spec — build the request manually.
            var request = new RequestInformation
            {
                HttpMethod = Method.GET,
                UrlTemplate = "{+baseurl}/api/v2/tickets/{ticket_id}/side_conversations{?page,per_page}"
            };
            request.PathParameters.Add("baseurl", requestAdapter.BaseUrl!);
            request.PathParameters.Add("ticket_id", ticketId);
            if (page is not null) request.QueryParameters.Add("page", page.Value);
            if (perPage is not null) request.QueryParameters.Add("per_page", perPage.Value);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "side_conversations", page, parsedDetail,
                MaxResponseChars("tickets_side_conversations_list"));
        });

    /// <summary>Exports SLA/metric events across tickets (breach timeline).</summary>
    [McpServerTool(Name = "tickets_metric_events_export", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Timestamped SLA/metric lifecycle event stream (apply_sla, breach, activate, fulfill, ...) for ALL tickets " +
        "with events at or after startTime (Unix epoch seconds). NO server-side ticket filter (and no per-ticket " +
        "variant) — for one ticket's timing use tickets_metrics_get; this is account-wide SLA analysis. Unlike " +
        "tickets_metrics_get (aggregate durations), shows WHEN an SLA target was applied or breached. Rows are " +
        "the raw event records (detail:'full' is a no-op — they have no leaner form). Page by passing the next " +
        "startTime the 'note' names (Zendesk's end_time) while 'has_more' is true; at end of stream the note " +
        "carries the startTime to resume from later. At most 100 records per page, chronological. Rate-limited by " +
        "Zendesk's incremental export API — avoid tight polling.")]
    public Task<JsonElement> MetricEvents(
        [Description(
            "Unix UTC epoch seconds; events recorded at or after this time, chronological.")]
        long startTime,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Built manually: the generated builder types start_time as int (breaking epochs past 2038) and its
            // response model drops the event fields on re-serialization — raw passthrough keeps the envelope
            // (ticket_metric_events/count/end_time/end_of_stream/next_page) intact.
            var request = new RequestInformation
            {
                HttpMethod = Method.GET,
                UrlTemplate = "{+baseurl}/api/v2/incremental/ticket_metric_events{?start_time}"
            };
            request.PathParameters.Add("baseurl", requestAdapter.BaseUrl!);
            request.QueryParameters.Add("start_time", startTime);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return BuildMetricEventsExportEnvelope(json, parsedDetail,
                MaxResponseChars("tickets_metric_events_export"));
        });

    /// <summary>Lists tickets.</summary>
    [McpServerTool(Name = "tickets_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Account tickets as lean summary rows (id, subject, 150-char description excerpt, status, priority, type, " +
        "dates, requester/assignee/group/organization ids, tags, via.channel) — detail:'full' for complete " +
        "records, or tickets_get for one. Archived tickets excluded (use tickets_export_incremental for full " +
        "history); has its own per-account rate limit — prefer tickets_search for filtered queries. Cursor " +
        "pagination: default pageSize 25 (max 100); response's has_more/after_cursor drive continuation. Sideloads " +
        "resolve related records inline (summary-projected).")]
    public Task<JsonElement> List(
        [Description("Results per page (default 25; Zendesk caps at 100).")]
        int? pageSize = 25,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for the first page. When present must be the opaque token " +
            "copied verbatim from the previous response's after_cursor; not a page number, must not be guessed or " +
            "passed empty (invalid cursor rejected with 400).")]
        string? afterCursor = null,
        [Description(
            "Sideloads resolving related records inline as sibling arrays (any of): \"users\", \"groups\", " +
            "\"organizations\", \"brands\", \"ticket_forms\", \"sharing_agreements\", \"custom_statuses\" " +
            "(human-readable custom-status labels), \"metric_sets\" (per-ticket reply/resolution timings), " +
            "\"dates\", \"last_audits\", \"incident_counts\". \"comment_count\" instead adds a comment_count field " +
            "to each row (not a sibling array). slas and metric_events are single-ticket only — use tickets_get " +
            "for those.")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Tickets
                .ToGetRequestInformation(configuration =>
                    configuration.QueryParameters.Include = Sideloads(include))
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "tickets", parsedDetail,
                MaxResponseChars("tickets_list"));
        });

    /// <summary>Returns many tickets by id in one call.</summary>
    [McpServerTool(Name = "tickets_get_many", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Many tickets by numeric id in one call — always prefer over repeated tickets_get for multiple ids. Hard " +
        "cap 100 ids/call (Zendesk's show_many limit) — for more, split into batches of 100 and call once per " +
        "batch. Rows are lean summaries — detail:'full' for complete records, or tickets_get for one. Sideloads: " +
        "\"users\", \"groups\", \"organizations\" resolve related records as sibling arrays; \"comment_count\" " +
        "adds comment_count to each row.")]
    public Task<JsonElement> ReadMany(
        [Description("Numeric Zendesk ticket ids (at most 100 per call).")]
        long[] ids,
        [Description(
            "Sideloads: \"users\", \"groups\", \"organizations\" returned as sibling arrays; \"comment_count\" " +
            "adds comment_count to each row.")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // show_many rejects more than 100 ids with 400 Bad Request — surface the API contract as an
            // actionable batching instruction instead of silently fanning out server-side.
            if (ids.Length > MaxIdsPerShowManyRequest)
                throw new McpException(
                    $"tickets_get_many accepts at most {MaxIdsPerShowManyRequest} ids per call (Zendesk's " +
                    $"show_many limit) but was passed {ids.Length}. Split the ids into batches of " +
                    $"{MaxIdsPerShowManyRequest} and call once per batch.");
            if (ids.Length == 0)
                return ZendeskLean.BuildOffsetListEnvelope(EmptyTicketsResponse(), "tickets", null,
                    parsedDetail, MaxResponseChars("tickets_get_many"));

            var request = zendesk.Api.V2.Tickets.Show_many.ToGetRequestInformation(configuration =>
            {
                configuration.QueryParameters.Ids = string.Join(',', ids);
                configuration.QueryParameters.Include = Sideloads(include);
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "tickets", null, parsedDetail,
                MaxResponseChars("tickets_get_many"), extraSummaryFields: CommentCountField(include));
        });

    /// <summary>Returns the account's total ticket count.</summary>
    [McpServerTool(Name = "tickets_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Account's total ticket count. Cached/approximate: above 100,000 tickets it refreshes only every ~24h and " +
        "stays capped at 100,000 until that background update completes ('refreshed_at' reports cache time, may be " +
        "null during that window). For a filtered subset's count use tickets_search and read 'count'.")]
    public Task<JsonElement> Count(CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Tickets.Count.ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("count", out var count)
                ? count
                : throw new McpException("Zendesk returned no ticket count.");
        });

    /// <summary>Returns the tickets carrying an external id.</summary>
    [McpServerTool(Name = "tickets_get_by_external_id", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Tickets carrying a given external_id — the link between Zendesk tickets and records in an outside system " +
        "(your CRM/order id, etc.). Multiple tickets can share one external id, so a list is returned. Rows are " +
        "lean summaries — detail:'full' for complete records, or tickets_get for one.")]
    public Task<JsonElement> ReadByExternalId(
        [Description("External id to look up (an identifier from your own system, not a Zendesk id).")]
        string externalId,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
            var request = zendesk.Api.V2.Tickets.ToGetRequestInformation(configuration =>
                configuration.QueryParameters.ExternalId = externalId);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "tickets", null, parsedDetail,
                MaxResponseChars("tickets_get_by_external_id"));
        });

    /// <summary>Lists the collaborators (CCs) of a ticket.</summary>
    [McpServerTool(Name = "tickets_collaborators_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Collaborators (CCs) of a ticket as user summary rows (id, name, email, role, ...) — who else is copied on " +
        "the conversation. Resolves the ticket's collaborator ids to users directly (no users_get follow-ups " +
        "needed); detail:'full' for complete user records.")]
    public Task<JsonElement> Collaborators(
        [Description("Numeric Zendesk ticket id.")]
        long ticketId,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Tickets[ticketId].Collaborators.ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "users", null, parsedDetail,
                MaxResponseChars("tickets_collaborators_list"));
        });

    /// <summary>Returns a ticket's comment count.</summary>
    [McpServerTool(Name = "tickets_comments_count", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Comment count of a ticket — cheaper than paging tickets_comments_list just to size the thread. " +
        "Cached/approximate (see 'refreshed_at' for freshness).")]
    public Task<JsonElement> CommentsCount(
        [Description("Numeric Zendesk ticket id.")]
        long ticketId,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var request = zendesk.Api.V2.Tickets[ticketId].Comments.Count.ToGetRequestInformation();
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("count", out var count)
                ? count
                : throw new McpException($"Zendesk returned no comment count for ticket '{ticketId}'.");
        });

    /// <summary>Exports tickets incrementally (cursor-based incremental export).</summary>
    [McpServerTool(Name = "tickets_export_incremental", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Cursor-based incremental export — the recommended way to sync full ticket history, including archived " +
        "tickets that tickets_list omits. Pass EXACTLY ONE of startTime (Unix epoch seconds, first call only) or " +
        "cursor (every subsequent call): start with startTime, then keep passing the response's 'after_cursor' as " +
        "cursor while 'has_more' is true; once caught up the 'note' carries the cursor for resuming later. Rows " +
        "are lean summaries (default perPage 100, max 1000) — detail:'full' for complete records. Admin-only, " +
        "rate-limited by Zendesk's incremental export API — avoid tight polling. Sideloads (\"users\", " +
        "\"groups\", \"organizations\") resolve related records inline; \"last_audits\" is NOT supported here.")]
    public Task<JsonElement> Incremental(
        [Description(
            "Unix epoch seconds for the FIRST call. Mutually exclusive with 'cursor' — pass exactly one. Zendesk " +
            "compares this against each ticket's generated_timestamp (not updated_at), so returned tickets may " +
            "have an updated_at earlier than startTime.")]
        long? startTime = null,
        [Description(
            "The 'after_cursor' from the previous page, for every call after the first. Mutually exclusive with 'startTime'.")]
        string? cursor = null,
        [Description(
            "Sideloads resolving inline as sibling arrays: any of \"users\", \"groups\", \"organizations\".")]
        string[]? include = null,
        [Description("Results per page (default 100, max 1000).")]
        int? perPage = 100,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // A blank cursor from an agent means "no cursor" (first call), not an empty cursor= on the wire.
            cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor;
            // Exactly one of the two: start_time begins the export, cursor continues it.
            if (startTime is null == cursor is null)
                throw new ArgumentException(
                    "Provide exactly one of startTime (initial call) or cursor (continuation).", nameof(startTime));

            // The generated builder types start_time as int (breaking epochs past 2038) and omits the include
            // and per_page parameters — supply them via the escape hatches ('start_time' is already in the
            // template; per_page is a recorded spec anomaly, see src/ES.FX.Zendesk/OpenApi/README.md).
            var request = zendesk.Api.V2.Incremental.Tickets.Cursor.ToGetRequestInformation(configuration =>
                    configuration.QueryParameters.Cursor = cursor)
                .WithQuery("per_page", perPage)
                .WithInclude(Sideloads(include));
            if (startTime is not null) request.QueryParameters.Add("start_time", startTime.Value);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return BuildIncrementalExportEnvelope(json, parsedDetail,
                MaxResponseChars("tickets_export_incremental"));
        });

    /// <summary>Lists soft-deleted (not-yet-archived) tickets.</summary>
    [McpServerTool(Name = "tickets_deleted_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists soft-deleted tickets (deleted but not yet archived/scrubbed — the last 30 days) as lean rows: id, " +
        "subject, actor (who deleted it), deleted_at, previous_state. This is the way to get the id that " +
        "tickets_restore / tickets_delete_permanently need. Ordered oldest→newest by ticket CREATED date (NOT by " +
        "deletion time), and archiving can shift the first row — so a just-deleted old ticket is NOT necessarily " +
        "on the last page. perPage default 25 (max 100); total in 'count'; 'has_more'/'next_page' drive paging. " +
        "Tighter rate limit than most reads (~10 req/min).")]
    public Task<JsonElement> DeletedList(
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description("Results per page (default 25, max 100). Total in 'count'; 'has_more'/'next_page' drive paging.")]
        int? perPage = 25,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            var request = zendesk.Api.V2.Deleted_tickets.ToGetRequestInformation(configuration =>
            {
                configuration.QueryParameters.Page = page;
                configuration.QueryParameters.PerPage = perPage;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "deleted_tickets", page, parsedDetail,
                MaxResponseChars("tickets_deleted_list"));
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>Builds the flat comma-separated sideload value, or <c>null</c> when nothing is requested.</summary>
    private static string? Sideloads(string[]? include) =>
        include is { Length: > 0 } ? string.Join(',', include) : null;

    /// <summary>
    ///     The <c>comment_count</c> extra summary field, kept on the rows when (and only when) the agent
    ///     explicitly requested that sideload — see <see cref="ZendeskLean.SummarizeEntity" />.
    /// </summary>
    private static string[]? CommentCountField(string[]? include) =>
        include?.Contains("comment_count", StringComparer.OrdinalIgnoreCase) is true ? ["comment_count"] : null;

    /// <summary>An empty wire-shaped tickets response, for the no-ids fast path of <c>tickets_get_many</c>.</summary>
    private static JsonElement EmptyTicketsResponse() =>
        JsonSerializer.SerializeToElement(new JsonObject { ["tickets"] = new JsonArray() });

    /// <summary>
    ///     Adapts the incremental export response to the lean cursor envelope. The export carries its
    ///     continuation at the TOP level (<c>after_cursor</c>/<c>end_of_stream</c>) instead of the <c>meta</c>
    ///     block <see cref="ZendeskLean.BuildCursorListEnvelope" /> reads, so an equivalent <c>meta</c> block is
    ///     synthesized (<c>has_more</c> = !<c>end_of_stream</c>). At end of stream the envelope reports
    ///     <c>has_more:false</c> and would drop the cursor — but Zendesk still issues one there, as the token
    ///     that resumes the export later — so it rides in the <c>note</c> instead.
    /// </summary>
    private static JsonElement BuildIncrementalExportEnvelope(JsonElement response, ZendeskDetail detail,
        int maxResponseChars)
    {
        if (response.ValueKind is not JsonValueKind.Object)
            throw new McpException("The Zendesk API returned an empty response where a payload was expected.");
        var source = (JsonObject)JsonNode.Parse(response.GetRawText())!;

        var endOfStream = source["end_of_stream"] is JsonValue endValue &&
                          endValue.TryGetValue(out bool ended) && ended;
        var afterCursor = source["after_cursor"] is JsonValue cursorValue &&
                          cursorValue.TryGetValue(out string? cursor) && !string.IsNullOrEmpty(cursor)
            ? cursor
            : null;
        source["meta"] = new JsonObject { ["has_more"] = !endOfStream, ["after_cursor"] = afterCursor };
        var note = endOfStream && afterCursor is not null
            ? $"end_of_stream reached — the export is caught up; re-call later with cursor:'{afterCursor}' to " +
              "pick up subsequent changes"
            : null;
        return ZendeskLean.BuildCursorListEnvelope(JsonSerializer.SerializeToElement(source), "tickets", detail,
            maxResponseChars, note);
    }

    /// <summary>
    ///     Adapts the metric-events incremental export to the lean cursor envelope. Like the ticket export it
    ///     carries its continuation at the TOP level (<c>end_time</c>/<c>end_of_stream</c>), so a <c>meta</c>
    ///     block is synthesized (<c>has_more</c> = !<c>end_of_stream</c>). The continuation is NOT a cursor but
    ///     the next <c>startTime</c> (Zendesk's <c>end_time</c>, Unix epoch seconds), so it is surfaced in the
    ///     <c>note</c> rather than as an <c>after_cursor</c> the tool cannot accept. <see cref="ZendeskLean" />
    ///     registers no <c>ticket_metric_events</c> summary shape (the records carry no <c>url</c> self-links and
    ///     have no leaner form), so the envelope is built in full mode over the pass-through records and the
    ///     <c>detail</c> echo is relabeled to what the agent asked for.
    /// </summary>
    private static JsonElement BuildMetricEventsExportEnvelope(JsonElement response, ZendeskDetail detail,
        int maxResponseChars)
    {
        if (response.ValueKind is not JsonValueKind.Object)
            throw new McpException("The Zendesk API returned an empty response where a payload was expected.");
        var source = (JsonObject)JsonNode.Parse(response.GetRawText())!;

        var endOfStream = source["end_of_stream"] is JsonValue endValue &&
                          endValue.TryGetValue(out bool ended) && ended;
        var endTime = source["end_time"] is JsonValue endTimeValue &&
                      endTimeValue.TryGetValue(out long endTimeSeconds)
            ? endTimeSeconds
            : (long?)null;
        // The continuation is the next startTime, not a cursor — keep after_cursor null so the envelope emits no
        // after_cursor, and route end_time through the note instead.
        source["meta"] = new JsonObject { ["has_more"] = !endOfStream, ["after_cursor"] = null };
        var note = endTime is { } next
            ? endOfStream
                ? $"end_of_stream reached — this export is caught up; re-call later with startTime:{next} to pick " +
                  "up subsequent events"
                : $"more events available — re-call with startTime:{next} for the next page"
            : null;

        var envelope = ZendeskLean.BuildCursorListEnvelope(JsonSerializer.SerializeToElement(source),
            "ticket_metric_events", ZendeskDetail.Full, maxResponseChars, note);
        if (detail is ZendeskDetail.Full) return envelope;

        var relabeled = (JsonObject)JsonNode.Parse(envelope.GetRawText())!;
        relabeled["detail"] = "summary";
        return JsonSerializer.SerializeToElement(relabeled);
    }

    /// <summary>
    ///     Parses and validates the comments <c>bodyFormat</c> parameter (<c>plain</c>/<c>rich</c>/<c>both</c>,
    ///     case-insensitive; a missing/blank value falls back to <c>plain</c>). Unknown values are rejected with
    ///     an <see cref="McpException" /> naming the allowed ones — never silently coerced.
    /// </summary>
    private static string ParseBodyFormat(string? bodyFormat)
    {
        if (string.IsNullOrWhiteSpace(bodyFormat)) return ZendeskCommentBodyFormats.Plain;
        var format = bodyFormat.Trim().ToLowerInvariant();
        return format is ZendeskCommentBodyFormats.Plain or ZendeskCommentBodyFormats.Rich
            or ZendeskCommentBodyFormats.Both
            ? format
            : throw new McpException(
                $"Invalid bodyFormat value '{bodyFormat}'. Allowed values: 'plain', 'rich' or 'both'.");
    }

    /// <summary>
    ///     Parses and validates the comments <c>order</c> parameter, returning <c>true</c> for newest-first.
    ///     Accepts <c>oldest</c> (the default) and <c>newest</c>, case-insensitively; anything else is rejected
    ///     with an <see cref="McpException" /> naming the allowed values — never silently coerced.
    /// </summary>
    private static bool ParseOrder(string? order)
    {
        if (string.IsNullOrWhiteSpace(order)) return false;
        return order.Trim().ToLowerInvariant() switch
        {
            "oldest" => false,
            "newest" => true,
            _ => throw new McpException(
                $"Invalid order value '{order}'. Allowed values: 'oldest' (default) or 'newest'.")
        };
    }

    /// <summary>
    ///     Parses and validates the comments <c>format</c> parameter, returning <c>true</c> for the markdown
    ///     transcript. Accepts <c>json</c> (the default) and <c>markdown</c>, case-insensitively; anything else is
    ///     rejected with an <see cref="McpException" /> naming the allowed values — never silently coerced.
    /// </summary>
    private static bool ParseCommentsFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format)) return false;
        return format.Trim().ToLowerInvariant() switch
        {
            CommentsFormatJson => false,
            CommentsFormatMarkdown => true,
            _ => throw new McpException(
                $"Invalid format value '{format}'. Allowed values: 'json' (default) or 'markdown'.")
        };
    }

    /// <summary>
    ///     Renders a projected comments page as a compact markdown transcript (one block per comment:
    ///     author/date/visibility header then the already-trimmed body), preserving the projection's
    ///     <c>order</c> and <c>maxBodyChars</c>. The paging metadata rides alongside as a JSON object
    ///     (<c>format</c>, <c>count</c>, <c>next_page</c>, <c>transcript</c>) so the agent can still walk the
    ///     thread; the per-comment ids that chain into other tools are deliberately dropped — use <c>format:'json'</c>
    ///     for those.
    /// </summary>
    private static JsonElement RenderCommentsMarkdown(ZendeskTicketCommentsResult result, int? page)
    {
        var authors = result.Users?
            .GroupBy(user => user.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var transcript = new StringBuilder();
        for (var index = 0; index < result.Comments.Count; index++)
        {
            var comment = result.Comments[index];
            if (index > 0) transcript.Append("\n\n");

            var author = comment.AuthorId is { } authorId && authors is not null &&
                         authors.TryGetValue(authorId, out var matched)
                ? matched.Name ?? matched.Email ?? $"user {authorId}"
                : comment.AuthorId is { } id
                    ? $"user {id}"
                    : "unknown author";
            var when = comment.CreatedAt?.ToString("u", CultureInfo.InvariantCulture) ?? "unknown date";
            var visibility = comment.Public ? "public" : "internal";

            transcript.Append(CultureInfo.InvariantCulture, $"### {author} · {when} · {visibility}\n");
            var body = comment.PlainBody ?? comment.Body;
            if (!string.IsNullOrWhiteSpace(body)) transcript.Append(body.Trim());
            if (comment.Attachments is { Count: > 0 } attachments)
                transcript.Append(CultureInfo.InvariantCulture,
                    $"\n[{attachments.Count} attachment(s): {string.Join(", ", attachments.Select(AttachmentLabel))}]");
        }

        // next_page carries the page NUMBER (not Zendesk's URL) to mirror the json envelope's offset contract.
        var envelope = new JsonObject
        {
            ["format"] = CommentsFormatMarkdown
        };
        if (result.Count is { } count) envelope["count"] = count;
        if (result.NextPage is not null)
        {
            envelope["has_more"] = true;
            envelope["next_page"] = (page ?? 1) + 1;
        }

        envelope["transcript"] = transcript.ToString();
        return JsonSerializer.SerializeToElement(envelope);
    }

    /// <summary>A short human label for a comment attachment in the markdown transcript.</summary>
    private static string AttachmentLabel(ZendeskTicketCommentAttachment attachment) =>
        attachment.FileName ?? attachment.ContentType ?? $"attachment {attachment.Id}";

    /// <summary>
    ///     Projects a comments page: one body representation unless <c>both</c> was requested, the per-comment
    ///     <paramref name="maxBodyChars" /> cap (0 = no limit) with a marker naming the exact single-comment
    ///     re-call (offset paging makes a comment addressable as <c>perPage:1, page:&lt;absolute index&gt;</c>,
    ///     computed here so the agent never has to), and empty attachments arrays omitted.
    /// </summary>
    private static ZendeskTicketCommentsResult ProjectComments(ZendeskTicketCommentsResult result, string format,
        int maxBodyChars, bool newestFirst, int? page, int? perPage)
    {
        // Zendesk applies its server default when the agent explicitly nulls perPage; mirror it so the marker's
        // absolute index addresses the right comment.
        var pageStart = ((page ?? 1) - 1) * (perPage ?? ZendeskServerDefaultPerPage);
        var comments = new List<ZendeskTicketComment>(result.Comments.Count);
        for (var index = 0; index < result.Comments.Count; index++)
        {
            var comment = result.Comments[index];
            comment = format switch
            {
                ZendeskCommentBodyFormats.Rich => comment with { PlainBody = null },
                ZendeskCommentBodyFormats.Plain => comment with { Body = null },
                _ => comment
            };
            if (maxBodyChars > 0)
            {
                var recovery = CommentRecovery(pageStart + index + 1, newestFirst);
                comment = comment with
                {
                    PlainBody = comment.PlainBody is null
                        ? null
                        : ZendeskLean.TruncateWithMarker(comment.PlainBody, maxBodyChars, recovery),
                    Body = comment.Body is null
                        ? null
                        : ZendeskLean.TruncateWithMarker(comment.Body, maxBodyChars, recovery)
                };
            }

            if (comment.Attachments is { Count: 0 }) comment = comment with { Attachments = null };
            comments.Add(comment);
        }

        return result with { Comments = comments };
    }

    /// <summary>
    ///     The truncation-marker recovery recipe: the exact re-call returning one comment in full. The absolute
    ///     index is only valid under the ordering it was computed in, so a newest-first read names its
    ///     <c>order</c> too.
    /// </summary>
    private static string CommentRecovery(int absoluteIndex, bool newestFirst) =>
        newestFirst
            ? $"re-call with maxBodyChars:0 (0 = no limit), order:'newest', perPage:1, page:{absoluteIndex} " +
              "for this comment"
            : $"re-call with maxBodyChars:0 (0 = no limit), perPage:1, page:{absoluteIndex} for this comment";

    /// <summary>Searches Zendesk tickets.</summary>
    [McpServerTool(Name = "tickets_search", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Searches tickets via Zendesk search syntax; auto-scoped to tickets. Results are lean summary rows (id, " +
        "subject, 150-char description excerpt, status, priority, type, dates, people ids, tags, result_type) — " +
        "detail:'full' for complete records, or tickets_get for one. For the latest/first N tickets, make a SINGLE " +
        "call: query \"created>2000-01-01\", sortBy \"created_at\", sortOrder \"desc\", perPage N. Do NOT fetch one " +
        "ticket at a time — perPage=1 across many pages returns the SAME tickets in N times the calls; default " +
        "perPage 25 (max 100), so set it to exactly the number you need. Sort via sortBy/sortOrder params, NOT " +
        "order_by:/sort: keywords in the query. Filter by channel with via: and the source NAME — Email channel is " +
        "\"via:mail\" (NOT channel:email or via:email; see query param). Total match count in 'count' (compare " +
        "against page length to tell 'few matches' from 'more pages available'). Caps at 1,000 total results; a " +
        "page past that returns 422 — for larger result sets use tickets_search_export.")]
    public Task<JsonElement> Search(
        [Description(
            "Zendesk search-syntax query (type:ticket added automatically). field:value terms combine with implicit " +
            "AND; repeat a field to OR its values (tags:silver tags:bronze); prefix a term with - to exclude. " +
            "Operators: : (equals), < > <= >=. Dates: YYYY-MM-DD (created>2024-01-01), relative amount " +
            "(created>4hours; units minutes|hours|days|weeks|months|years), or full ISO8601 " +
            "(created>2015-09-01T12:00:00-08:00). Channel filter: 'via:' SOURCE keyword, value is the source NAME " +
            "not the channel string — Email is 'via:mail' (NOT via:email, channel:email, via.channel:email, or " +
            "via_id:4 — none are operators; Zendesk silently fuzzy-matches them to full text, giving false " +
            "positives and wrong counts). Other sources: via:web, via:api, via:chat. 'via:' is approximate — " +
            "confirm via.channel on each result (detail:'full' or tickets_get_many) to drop look-alikes such as a " +
            "native_messaging ticket that fuzzy-matched via:mail. Field selectors ONLY — no sort/order operators; " +
            "order with sortBy/sortOrder. Common selectors and accepted values: status: " +
            "new|open|pending|hold|solved|closed (ordered — status<solved, status>=pending work); priority: " +
            "low|normal|high|urgent (ordered); ticket_type: question|incident|problem|task; " +
            "assignee/requester/submitter/cc/commenter: numeric user id, full/partial name, email, or phone, plus " +
            "tokens 'none' (empty) and 'me' (API user) — assignee:none, requester:me; group: group NAME or numeric " +
            "id (group:\"Level 2\") — NOT email; organization: org name or id, or 'none'; brand: brand name or id; " +
            "form:\"<form name>\" (ticket form, name or id; quote multi-word names); " +
            "tags: tag name or 'none' (tags:a tags:b = a OR b; tags:\"a b\" one quoted value = requires BOTH); " +
            "subject:/description:/comment: free text (quote multi-word phrases); has_attachment: true|false; " +
            "recipient: the support address; custom_field_<numeric-field-id>:value for a specific custom field, or " +
            "fieldvalue:<value> to match any custom field (decode field ids with ticket_fields_list); support_type: " +
            "ai_agent|agent (messaging tickets only).")]
        string query,
        [Description("Sort field: created_at|updated_at|priority|status|ticket_type (optional).")]
        string? sortBy = null,
        [Description("Sort order: asc|desc; defaults to desc (optional).")]
        string? sortOrder = null,
        [Description(
            "1-based page number (optional). Only to page THROUGH a result set larger than perPage; to get the " +
            "first/latest N, raise perPage instead of repeated single-result page calls.")]
        int? page = null,
        [Description(
            "Tickets to return in THIS single call (default 25, max 100). For the latest/first N, set to N and call " +
            "once. Total match count in 'count'.")]
        int? perPage = 25,
        [Description(
            "Sideloads resolving ids inline as sibling arrays: any of \"users\", \"groups\", \"organizations\".")]
        string[]? include = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            ArgumentException.ThrowIfNullOrWhiteSpace(query);

            // Scope the search to tickets unless the caller already supplied a `type:` result-type selector.
            // Match `type:` only at a token boundary so unrelated qualifiers (ticket_type:, support_type:,
            // content-type:, or free text) do not disable scoping and leave the search across all record types.
            var scopedQuery = TypeSelectorRegex().IsMatch(query) ? query : $"type:ticket {query}".Trim();

            // The Search API sideloads with the nested `include=tickets(users,organizations)` syntax (unlike
            // list endpoints, which use a flat list). The nested value shape is doc-only — the OAS models a flat
            // list: https://developer.zendesk.com/documentation/ticketing/using-the-zendesk-api/side_loading/
            // (recorded in the spec-anomaly ledger, src/ES.FX.Zendesk/OpenApi/README.md).
            var scopedInclude = include is { Length: > 0 } ? $"tickets({string.Join(',', include)})" : null;

            // The published spec omits page/per_page on /api/v2/search — the live API supports them.
            var request = zendesk.Api.V2.Search.ToGetRequestInformation(configuration =>
                {
                    configuration.QueryParameters.Query = scopedQuery;
                    if (sortBy is not null) configuration.QueryParameters.SortBy = sortBy;
                    if (sortOrder is not null) configuration.QueryParameters.SortOrder = sortOrder;
                    if (scopedInclude is not null) configuration.QueryParameters.Include = scopedInclude;
                })
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildOffsetListEnvelope(json, "results", page, parsedDetail,
                MaxResponseChars("tickets_search"));
        });

    /// <summary>Exports ticket search results with cursor pagination (no 1,000-result cap).</summary>
    [McpServerTool(Name = "tickets_search_export", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Cursor-only deep export of ticket search results. Unlike tickets_search there is NO 1,000-result cap — " +
        "use this for large result sets. No type: selector needed — the ticket type filter is applied. Rows are " +
        "lean summaries — detail:'full' for complete records. Cursor pagination: default pageSize 100 (max 1000 — " +
        "large pages can time out); response's has_more/after_cursor drive continuation. Cursors expire after one " +
        "hour.")]
    public Task<JsonElement> SearchExport(
        [Description(
            "Zendesk search query (ticket type filter applied automatically). Do NOT include a type: selector — it " +
            "errors here. Results ordered only by created_at. Channel filter: 'via:' source keyword — Email is " +
            "'via:mail' (NOT channel:email or via:email, which are not operators); confirm via.channel on results " +
            "(detail:'full') to drop fuzzy look-alikes. Same ticket-search field syntax as tickets_search: " +
            "status/priority/ticket_type enums; assignee/requester/group/organization by id, name, email, or " +
            "none/me; form:\"<form name>\" (ticket form, name or id; quote multi-word names); tags (repeat to OR, " +
            "quote \"a b\" to require both); has_attachment; custom_field_<id>:value " +
            "and fieldvalue:<value>; date fields created/updated/solved/due_date with YYYY-MM-DD, ISO8601, or " +
            "relative amounts like created>4hours; operators : < > <= >=.")]
        string query,
        [Description("Cursor page size (default 100, max 1000 — Zendesk recommends 100; 1000/page can time out).")]
        int? pageSize = 100,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for the first page. When present must be the opaque token " +
            "copied verbatim from the previous response's after_cursor; not a page number, must not be guessed or " +
            "passed empty (invalid cursor rejected with 400).")]
        string? afterCursor = null,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            ArgumentException.ThrowIfNullOrWhiteSpace(query);

            var request = zendesk.Api.V2.Search.Export.ToGetRequestInformation(configuration =>
            {
                configuration.QueryParameters.Query = query;
                configuration.QueryParameters.Filtertype = "ticket";
                if (pageSize is not null) configuration.QueryParameters.Pagesize = pageSize;
                // Blank (not just null) means "first page" — an empty page[after] is rejected with a 400.
                if (!string.IsNullOrWhiteSpace(afterCursor)) configuration.QueryParameters.Pageafter = afterCursor;
            });
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            return ZendeskLean.BuildCursorListEnvelope(json, "results", parsedDetail,
                MaxResponseChars("tickets_search_export"));
        });

    [GeneratedRegex(@"(^|\s)type:", RegexOptions.IgnoreCase)]
    private static partial Regex TypeSelectorRegex();
}

/// <summary>
///     A page of ticket comments (<c>GET /api/v2/tickets/{id}/comments</c>) — the curated, token-economical
///     projection <c>tickets_comments_list</c> returns after applying its <c>bodyFormat</c> body trimming and
///     the per-comment <c>maxBodyChars</c> cap.
/// </summary>
public sealed record ZendeskTicketCommentsResult
{
    [JsonPropertyName("comments")] public IReadOnlyList<ZendeskTicketComment> Comments { get; init; } = [];
    [JsonPropertyName("count")] public int? Count { get; init; }
    [JsonPropertyName("next_page")] public string? NextPage { get; init; }

    /// <summary>Sideloaded comment authors (populated only when the request asks to include <c>users</c>).</summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<ZendeskTicketCommentAuthor>? Users { get; init; }
}

/// <summary>
///     A comment on a Zendesk ticket (the conversation thread). <see cref="Public" /> distinguishes an
///     agent/end-user visible reply (<c>true</c>) from an internal note (<c>false</c>).
/// </summary>
public sealed record ZendeskTicketComment
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("author_id")] public long? AuthorId { get; init; }

    /// <summary>The comment body as plain text.</summary>
    [JsonPropertyName("plain_body")]
    public string? PlainBody { get; init; }

    /// <summary>The comment body (may contain markup). Prefer <see cref="PlainBody" /> for plain text.</summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary><c>true</c> for a public reply; <c>false</c> for an internal note.</summary>
    [JsonPropertyName("public")]
    public bool Public { get; init; }

    [JsonPropertyName("attachments")] public IReadOnlyList<ZendeskTicketCommentAttachment>? Attachments { get; init; }

    [JsonPropertyName("via")] public ZendeskTicketCommentVia? Via { get; init; }
    [JsonPropertyName("audit_id")] public long? AuditId { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>An attachment on a Zendesk ticket comment.</summary>
public sealed record ZendeskTicketCommentAttachment
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("file_name")] public string? FileName { get; init; }
    [JsonPropertyName("content_url")] public string? ContentUrl { get; init; }
    [JsonPropertyName("content_type")] public string? ContentType { get; init; }
    [JsonPropertyName("size")] public long? Size { get; init; }
    [JsonPropertyName("inline")] public bool? Inline { get; init; }
}

/// <summary>Describes how a ticket comment entered the system (its channel).</summary>
public sealed record ZendeskTicketCommentVia
{
    [JsonPropertyName("channel")] public string? Channel { get; init; }
}

/// <summary>A sideloaded comment author (a Zendesk user).</summary>
public sealed record ZendeskTicketCommentAuthor
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("role")] public string? Role { get; init; }
    [JsonPropertyName("active")] public bool Active { get; init; }
    [JsonPropertyName("verified")] public bool Verified { get; init; }
    [JsonPropertyName("organization_id")] public long? OrganizationId { get; init; }
    [JsonPropertyName("time_zone")] public string? TimeZone { get; init; }
    [JsonPropertyName("locale")] public string? Locale { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
}