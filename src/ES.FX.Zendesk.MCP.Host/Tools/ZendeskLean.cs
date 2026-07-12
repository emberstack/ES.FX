using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     The response detail level a list/search/sublist tool applies to its rows (see
///     <see cref="ZendeskLean.ParseDetail" />).
/// </summary>
internal enum ZendeskDetail
{
    /// <summary>Lean allowlist summary rows — the default.</summary>
    Summary,

    /// <summary>Complete Zendesk objects, minus API self-links and null-valued fields.</summary>
    Full
}

/// <summary>
///     The lean-first projection layer for list/search/sublist tool responses (successor to the retired
///     denylist-based <c>ZendeskToolProjection</c>): per-entity <b>allowlist</b> summary shapes, a full-view transform
///     (complete objects minus API self-links and nulls), a uniform metadata-first list envelope, and the
///     response-size guard. Summary rows carry only the fields an agent needs to triage or pick a record; the
///     complete record stays reachable via <c>detail:'full'</c> or the per-record <c>*_get</c> tools. Dynamic
///     conditions (continuation, truncation, omitted sideloads) ride in the envelope's <c>note</c>.
/// </summary>
internal static partial class ZendeskLean
{
    /// <summary>Summary-row truncation length for a ticket's <c>description</c>.</summary>
    private const int TicketDescriptionChars = 150;

    /// <summary>Summary-row truncation length for excerpts and descriptions (audits, side conversations, sections).</summary>
    private const int ExcerptChars = 200;

    /// <summary>The maximum number of per-item failures embedded in a job status <c>results_summary</c>.</summary>
    private const int MaxJobFailuresInSummary = 5;

    /// <summary>
    ///     The per-entity allowlist summary shapes, keyed by the Zendesk envelope array name (which acts as the
    ///     type label for primary and sideloaded arrays alike). Heterogeneous <c>results</c> arrays (search) are
    ///     not listed here — they dispatch per item on <c>result_type</c>.
    /// </summary>
    private static readonly Dictionary<string, Func<JsonObject, JsonObject>> SummaryShapes =
        new(StringComparer.Ordinal)
        {
            ["tickets"] = SummarizeTicket,
            ["users"] = SummarizeUser,
            ["organizations"] = SummarizeOrganization,
            ["articles"] = SummarizeArticle,
            ["groups"] = SummarizeGroup,
            ["macros"] = SummarizeMacro,
            ["views"] = SummarizeView,
            ["ticket_fields"] = SummarizeTicketField,
            ["ticket_forms"] = SummarizeTicketForm,
            ["brands"] = SummarizeBrand,
            ["custom_statuses"] = SummarizeCustomStatus,
            ["suspended_tickets"] = SummarizeSuspendedTicket,
            ["identities"] = SummarizeIdentity,
            ["attachments"] = SummarizeAttachment,
            ["side_conversations"] = SummarizeSideConversation,
            ["job_statuses"] = SummarizeJobStatus,
            ["audits"] = SummarizeAudit,
            ["sections"] = SummarizeSectionOrCategory,
            ["categories"] = SummarizeSectionOrCategory,
            ["satisfaction_ratings"] = SummarizeSatisfactionRating,
            ["deleted_tickets"] = SummarizeDeletedTicket,
            ["community_posts"] = SummarizeCommunityPost,
            ["custom_objects"] = SummarizeCustomObject,
            ["custom_object_records"] = SummarizeCustomObjectRecord
        };

    /// <summary>
    ///     Maps a search row's <c>result_type</c> to its summary-shape key. Rows with an unmapped
    ///     <c>result_type</c> fail visibly: returned in full view with an explanatory note.
    /// </summary>
    private static readonly Dictionary<string, string> SearchResultShapes = new(StringComparer.Ordinal)
    {
        ["ticket"] = "tickets",
        ["user"] = "users",
        ["organization"] = "organizations",
        ["group"] = "groups"
    };

    /// <summary>
    ///     The declarative statement of the per-entity summary allowlists: for each shape in
    ///     <see cref="SummaryShapes" />, the <b>source</b> (wire) field names the shape reads off the Zendesk
    ///     entity. This map exists for the OAS-coupled staleness tests (design A6): the existence test pins every
    ///     field here to a property of the vendored OpenAPI schema (so a Zendesk rename/removal fails at
    ///     re-vendor time), and the classification snapshot forces every schema property to be triaged as
    ///     summary|omitted. Keep it in exact sync with the <c>Summarize*</c> methods — the shape-coherence test
    ///     feeds every field listed here through its shape and fails on any mismatch in either direction.
    ///     Per-call extras (search rows' <c>result_type</c>, sideload-materialized fields such as
    ///     <c>comment_count</c>) and computed outputs (<c>options_count</c>, <c>results_summary</c>, event
    ///     <c>excerpt</c>s) are deliberately not listed: only fields read from the entity's top level are.
    /// </summary>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> SummarySourceFields { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["tickets"] =
            [
                "id", "subject", "description", "status", "priority", "type", "due_at", "created_at",
                "updated_at", "requester_id", "assignee_id", "group_id", "organization_id", "custom_status_id",
                "ticket_form_id", "problem_id", "external_id", "tags", "via"
            ],
            ["users"] =
            [
                "id", "name", "email", "role", "active", "suspended", "organization_id", "phone",
                "last_login_at", "external_id"
            ],
            ["organizations"] =
            [
                "id", "name", "domain_names", "external_id", "shared_tickets", "shared_comments", "tags",
                "created_at", "updated_at"
            ],
            ["articles"] =
            [
                "id", "title", "html_url", "section_id", "locale", "draft", "promoted", "label_names",
                "updated_at", "snippet"
            ],
            ["groups"] = ["id", "name", "description", "default", "deleted", "is_public"],
            ["macros"] = ["id", "title", "active", "description", "usage_7d", "usage_30d"],
            ["views"] = ["id", "title", "active", "default", "position"],
            // custom/system_field_options are consumed sources: they collapse to the computed options_count.
            ["ticket_fields"] =
                ["id", "type", "title", "active", "required", "custom_field_options", "system_field_options"],
            ["ticket_forms"] = ["id", "name", "active", "default", "position", "ticket_field_ids"],
            ["brands"] = ["id", "name", "subdomain", "active", "default", "has_help_center"],
            ["custom_statuses"] = ["id", "status_category", "agent_label", "active"],
            ["suspended_tickets"] = ["id", "subject", "cause", "author", "brand_id", "ticket_id", "created_at"],
            ["identities"] = ["id", "user_id", "type", "value", "primary", "verified"],
            ["attachments"] = ["id", "file_name", "content_type", "size", "inline", "malware_scan_result"],
            ["side_conversations"] =
                ["id", "subject", "state", "created_at", "message_added_at", "participants", "preview_text"],
            // `results` is a consumed source: it collapses to the computed results_summary.
            ["job_statuses"] = ["id", "status", "progress", "total", "message", "results"],
            ["audits"] = ["id", "created_at", "author_id", "via", "events"],
            ["sections"] =
            [
                "id", "name", "html_url", "description", "category_id", "parent_section_id", "position",
                "updated_at"
            ],
            // Categories share the section shape; category_id/parent_section_id can never materialize on a
            // category (they are not CategoryObject properties), so the category allowlist omits them.
            ["categories"] = ["id", "name", "html_url", "description", "position", "updated_at"],
            // CSAT ratings are already small; the allowlist keeps everything triage-relevant and omits only the
            // API self-link (url) and the redundant reason_id (reason + reason_code carry the reason).
            ["satisfaction_ratings"] =
            [
                "id", "score", "comment", "reason", "reason_code", "ticket_id", "requester_id", "assignee_id",
                "group_id", "created_at", "updated_at"
            ],
            // Deleted-ticket rows are an inline schema (no named component in the OAS), so this entity is exempt
            // from the schema-existence/classification tests (like side_conversations). The rows are already
            // minimal — actor is {id,name}; there are no bodies to strip.
            ["deleted_tickets"] = ["id", "subject", "actor", "deleted_at", "previous_state"],
            // Community (Gather) post triage rows; the post body (details) and low-signal metadata are omitted.
            ["community_posts"] =
            [
                "id", "title", "html_url", "status", "author_id", "topic_id", "created_at", "updated_at",
                "comment_count", "vote_sum", "pinned", "closed"
            ],
            // Custom object TYPE metadata (needed to discover the key used to query records); raw_* localization
            // duplicates and admin flags are omitted.
            ["custom_objects"] = ["key", "title", "title_pluralized", "description", "created_at", "updated_at"],
            // Custom object RECORDS — the tenant business data linked to tickets/users. custom_object_fields IS the
            // payload (kept verbatim); only the self-link and the photo attachment are stripped.
            ["custom_object_records"] =
            [
                "id", "name", "custom_object_key", "external_id", "custom_object_fields", "created_at",
                "updated_at", "created_by_user_id", "updated_by_user_id"
            ]
        };

    /// <summary>The registered summary-shape keys, exposed for the staleness tests (design A6).</summary>
    internal static IEnumerable<string> SummaryShapeNames => SummaryShapes.Keys;

    /// <summary>
    ///     Parses and validates a tool's <c>detail</c> parameter. Accepts <c>summary</c> (alias <c>concise</c>)
    ///     and <c>full</c> (aliases <c>detailed</c>, <c>verbose</c>), case-insensitively; a missing/blank value
    ///     falls back to the default (<see cref="ZendeskDetail.Summary" />). Anything else is rejected with an
    ///     <see cref="McpException" /> naming the allowed values — never silently coerced.
    /// </summary>
    public static ZendeskDetail ParseDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return ZendeskDetail.Summary;
        return detail.Trim().ToLowerInvariant() switch
        {
            "summary" or "concise" => ZendeskDetail.Summary,
            "full" or "detailed" or "verbose" => ZendeskDetail.Full,
            _ => throw new McpException(
                $"Invalid detail value '{detail}'. Allowed values: 'summary' (alias 'concise') or 'full' " +
                "(aliases 'detailed', 'verbose').")
        };
    }

    /// <summary>
    ///     Whether a summary shape is registered for the given Zendesk array name (including the heterogeneous
    ///     <c>results</c> search array, which dispatches per item).
    /// </summary>
    public static bool HasSummaryShape(string arrayName) =>
        arrayName == "results" || SummaryShapes.ContainsKey(arrayName);

    /// <summary>
    ///     Projects a single entity to its allowlist summary row, or returns <c>null</c> when no summary shape is
    ///     registered for <paramref name="arrayName" />. Fields listed in <paramref name="extraFields" /> — the
    ///     per-call escape hatch for fields materialized by an explicitly requested sideload (for example
    ///     <c>comment_count</c>) — are appended verbatim to the row when present on the entity.
    /// </summary>
    public static JsonObject? SummarizeEntity(string arrayName, JsonObject entity,
        IReadOnlyCollection<string>? extraFields = null)
    {
        if (!SummaryShapes.TryGetValue(arrayName, out var shape)) return null;
        var row = shape(entity);
        AppendExtraFields(entity, row, extraFields);
        return row;
    }

    /// <summary>
    ///     The full-view transform for <c>detail:'full'</c> rows and <c>*_get</c> responses: the complete Zendesk
    ///     object minus <c>url</c> API self-links (<c>html_url</c>, the human permalink, is always kept),
    ///     null-valued properties, absolute <c>next_page</c>/<c>previous_page</c> URL strings, and cursor
    ///     <c>links</c> blocks. Absence convention (stated in tool guidance): an absent field means null/empty,
    ///     not unknown.
    /// </summary>
    public static JsonElement ToFullView(JsonElement element)
    {
        var transformed = ToFullViewNode(JsonNode.Parse(element.GetRawText()));
        return JsonSerializer.SerializeToElement(transformed);
    }

    /// <summary>
    ///     Builds the uniform lean list envelope for a <b>cursor-paginated</b> endpoint, reading
    ///     <c>meta.has_more</c>/<c>meta.after_cursor</c> from the Zendesk response. See
    ///     <see cref="BuildListEnvelope" /> for the envelope contract.
    /// </summary>
    /// <param name="response">The raw Zendesk list envelope.</param>
    /// <param name="itemsArrayName">The primary array's Zendesk name (for example <c>tickets</c>).</param>
    /// <param name="detail">The validated row detail level.</param>
    /// <param name="maxResponseChars">The response-size budget (see <c>McpToolsOptions.GetMaxResponseChars</c>).</param>
    /// <param name="note">An optional tool-supplied note (dynamic conditions only), merged into the envelope note.</param>
    /// <param name="extraSummaryFields">Row fields materialized by an explicitly requested sideload, kept in summary rows.</param>
    /// <param name="itemShapeName">
    ///     Overrides the summary-shape key for the primary rows when it differs from
    ///     <paramref name="itemsArrayName" /> (for example Help Center search returns articles in <c>results</c>).
    /// </param>
    public static JsonElement BuildCursorListEnvelope(JsonElement response, string itemsArrayName,
        ZendeskDetail detail, int maxResponseChars, string? note = null,
        IReadOnlyCollection<string>? extraSummaryFields = null, string? itemShapeName = null) =>
        BuildListEnvelope(response, itemsArrayName, itemShapeName, detail, true,
            null, maxResponseChars, note, extraSummaryFields);

    /// <summary>
    ///     Builds the uniform lean list envelope for an <b>offset-paginated</b> endpoint. When Zendesk's
    ///     <c>next_page</c> is non-null the envelope carries <c>next_page</c> as a page <em>number</em> computed
    ///     as (<paramref name="requestPage" /> ?? 1) + 1 — Zendesk's URL strings are never parsed or echoed. See
    ///     <see cref="BuildListEnvelope" /> for the envelope contract.
    /// </summary>
    /// <param name="response">The raw Zendesk list envelope.</param>
    /// <param name="itemsArrayName">The primary array's Zendesk name (for example <c>results</c>).</param>
    /// <param name="requestPage">The page number the tool requested (<c>null</c> when the agent omitted it).</param>
    /// <param name="detail">The validated row detail level.</param>
    /// <param name="maxResponseChars">The response-size budget (see <c>McpToolsOptions.GetMaxResponseChars</c>).</param>
    /// <param name="note">An optional tool-supplied note (dynamic conditions only), merged into the envelope note.</param>
    /// <param name="extraSummaryFields">Row fields materialized by an explicitly requested sideload, kept in summary rows.</param>
    /// <param name="itemShapeName">
    ///     Overrides the summary-shape key for the primary rows when it differs from
    ///     <paramref name="itemsArrayName" />.
    /// </param>
    public static JsonElement BuildOffsetListEnvelope(JsonElement response, string itemsArrayName,
        int? requestPage, ZendeskDetail detail, int maxResponseChars, string? note = null,
        IReadOnlyCollection<string>? extraSummaryFields = null, string? itemShapeName = null) =>
        BuildListEnvelope(response, itemsArrayName, itemShapeName, detail, false,
            requestPage, maxResponseChars, note, extraSummaryFields);

    /// <summary>
    ///     Projects a Zendesk view <c>execute</c> response (<c>{ columns, groups, rows, view }</c>) into the lean
    ///     envelope. The configured <c>columns</c> layout metadata is kept (that is the point of executing a view
    ///     rather than listing its tickets), each <c>rows</c> entry keeps its scalar column values but replaces the
    ///     embedded heavy <c>ticket</c> object with a lean ticket summary (or full view under <c>detail:'full'</c>),
    ///     and Zendesk's <c>next_page</c> URL collapses to a page <em>number</em> (never echoed). Over-budget
    ///     responses raise the recoverable size-guard <see cref="McpException" /> (page through it) — the row set is
    ///     bounded by the view's fixed page size, so tail-dropping is unnecessary here.
    /// </summary>
    public static JsonElement BuildViewExecuteEnvelope(JsonElement response, int? requestPage, ZendeskDetail detail,
        int maxResponseChars)
    {
        if (response.ValueKind is not JsonValueKind.Object)
            throw new InvalidOperationException("The Zendesk view execute response was not a JSON object.");
        var source = (JsonObject)JsonNode.Parse(response.GetRawText())!;

        var items = new JsonArray();
        if (source["rows"] is JsonArray rows)
            foreach (var row in rows)
                items.Add(ProjectViewRow(row, detail));

        var envelope = new JsonObject { ["detail"] = detail is ZendeskDetail.Full ? "full" : "summary" };
        if (source["count"] is JsonValue countValue && countValue.TryGetValue(out long count))
            envelope["count"] = count;
        if (source["next_page"] is not null)
        {
            envelope["has_more"] = true;
            envelope["next_page"] = (requestPage ?? 1) + 1;
        }

        // The view's configured column layout (id + title per column) — small; full-viewed to drop any self-links.
        // The grouping metadata is intentionally not echoed: each row already carries its own `group` scalar.
        if (source["columns"] is JsonArray columns) envelope["columns"] = ToFullViewNode(columns);
        envelope["items"] = items;

        return EnsureWithinBudget(JsonSerializer.SerializeToElement(envelope), "views_rows_list", maxResponseChars,
            "View page is large — request a later page (page:N) or read individual tickets via tickets_get.");
    }

    /// <summary>
    ///     Projects a Help Center deflection-suggestions response (<c>{ results: [{ name, html_url }] }</c>) into
    ///     the lean envelope. Each suggested-article row carries only its title (<c>name</c>) and the human
    ///     permalink (<c>html_url</c>) — the endpoint returns no id or body — so the projection is a fixed two-field
    ///     allowlist. Relevance-ordered; the endpoint has no pagination.
    /// </summary>
    public static JsonElement BuildDeflectionEnvelope(JsonElement response, int maxResponseChars)
    {
        var items = new JsonArray();
        if (response.ValueKind is JsonValueKind.Object && response.TryGetProperty("results", out var results) &&
            results.ValueKind is JsonValueKind.Array)
            foreach (var node in (JsonArray)JsonNode.Parse(results.GetRawText())!)
            {
                if (node is not JsonObject row) continue;
                var projected = new JsonObject();
                Copy(row, projected, "name", "html_url");
                items.Add(projected);
            }

        var envelope = new JsonObject { ["detail"] = "summary", ["items"] = items };
        return EnsureWithinBudget(JsonSerializer.SerializeToElement(envelope), "articles_deflection_search",
            maxResponseChars, "Too many suggestions — use a shorter, more specific query.");
    }

    /// <summary>
    ///     Projects one view-execute row: scalar column values are kept, the embedded <c>ticket</c> is summarized
    ///     (or full-viewed), and a row-level <c>url</c> self-link is dropped. Non-object rows pass through.
    /// </summary>
    private static JsonNode? ProjectViewRow(JsonNode? row, ZendeskDetail detail)
    {
        if (row is not JsonObject rowObject) return row?.DeepClone();
        var projected = new JsonObject();
        foreach (var (name, value) in rowObject)
            switch (name)
            {
                case "ticket" when value is JsonObject ticket:
                    projected["ticket"] = detail is ZendeskDetail.Summary
                        ? SummarizeEntity("tickets", ticket)
                        : ToFullViewNode(ticket);
                    break;
                case "url" when value is JsonValue urlValue && urlValue.TryGetValue<string>(out _):
                    break; // drop the row self-link
                default:
                    projected[name] = detail is ZendeskDetail.Full ? ToFullViewNode(value) : value?.DeepClone();
                    break;
            }

        return projected;
    }

    /// <summary>
    ///     Applies the envelope's summary-mode <b>sideload contract</b> to a raw Zendesk list response in place,
    ///     for tools whose PRIMARY rows have no <see cref="SummaryShapes" /> entry and therefore assemble their
    ///     envelope in full mode (bespoke or pass-through rows: group/organization memberships, tags): every
    ///     array other than <paramref name="itemsArrayName" /> is summary-projected through its registered
    ///     shape, and an array without one is removed — failing visibly through the returned note
    ///     (<c>"sideload X has no summary shape — use detail:'full'"</c>). Returns <c>null</c> when there is
    ///     nothing to report.
    /// </summary>
    public static string? ApplySummarySideloadContract(JsonObject source, string itemsArrayName)
    {
        var notes = new List<string>();
        foreach (var (name, value) in source.ToList())
        {
            if (name == itemsArrayName || value is not JsonArray sideload) continue;
            // Match BuildListEnvelope: `results` dispatches per item only as a PRIMARY array — as a sideload it
            // has no shape and is omitted like any other unknown array.
            if (!SummaryShapes.ContainsKey(name))
            {
                source.Remove(name);
                notes.Add($"sideload {name} has no summary shape — use detail:'full'");
                continue;
            }

            var projected = new JsonArray();
            foreach (var item in sideload)
                projected.Add(item is JsonObject entity ? SummarizeEntity(name, entity) : item?.DeepClone());
            source[name] = projected;
        }

        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    /// <summary>
    ///     The response-size guard for <b>non-list</b> tools (list tools truncate inside
    ///     <see cref="BuildListEnvelope" /> instead): returns the response unchanged when its serialized length
    ///     fits <paramref name="maxResponseChars" />, otherwise throws an <see cref="McpException" /> naming the
    ///     tool and the caller-supplied escalation recipe (that tool's actual narrowing parameters). Tools with
    ///     their own explicit size caps (for example <c>attachments_get</c>'s byte cap) are exempt by design and
    ///     must not call this.
    /// </summary>
    public static JsonElement EnsureWithinBudget(JsonElement response, string toolName, int maxResponseChars,
        string escalationHint)
    {
        var length = response.GetRawText().Length;
        if (length <= maxResponseChars) return response;
        throw new McpException(
            $"The {toolName} response is {length} characters, over the {maxResponseChars}-character response " +
            $"limit. {escalationHint}");
    }

    /// <summary>
    ///     Truncates <paramref name="value" /> to at most <paramref name="maxChars" /> characters with a plain
    ///     trailing ellipsis — the summary-row style for excerpts and descriptions. Never splits a surrogate pair.
    ///     Returns the value unchanged when it already fits.
    /// </summary>
    public static string Truncate(string value, int maxChars)
    {
        if (value.Length <= maxChars) return value;
        return value[..KeptChars(value, maxChars)] + "…";
    }

    /// <summary>
    ///     Truncates <paramref name="value" /> with the explicit self-describing marker style
    ///     (<c>…[truncated N chars — {recovery}]</c>), where <paramref name="recovery" /> names the exact re-call
    ///     that retrieves the untruncated content. Returns the value unchanged when it already fits.
    /// </summary>
    public static string TruncateWithMarker(string value, int maxChars, string recovery)
    {
        if (value.Length <= maxChars) return value;
        var kept = KeptChars(value, maxChars);
        return $"{value[..kept]}…[truncated {value.Length - kept} chars — {recovery}]";
    }

    /// <summary>
    ///     Best-effort HTML-to-plain-text conversion for <c>bodyFormat:'plain'</c> (Help Center article bodies):
    ///     drops script/style blocks, turns block-level tags into newlines, strips the remaining tags, decodes
    ///     HTML entities, and collapses whitespace. Deliberately dependency-free — this is a token-economy
    ///     transform, not an HTML parser.
    /// </summary>
    public static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        var text = ScriptStyleRegex().Replace(html, string.Empty);
        text = LineBreakTagRegex().Replace(text, "\n");
        text = BlockTagRegex().Replace(text, "\n");
        text = AnyTagRegex().Replace(text, string.Empty);
        // Decode entities AFTER stripping tags so literal markup in text ("&lt;b&gt;") is not re-parsed as a tag.
        text = WebUtility.HtmlDecode(text);
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = HorizontalWhitespaceRegex().Replace(text, " ");
        text = SpaceAroundNewlineRegex().Replace(text, "\n");
        text = NewlineRunRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>
    ///     The shared envelope builder. The envelope is <b>metadata first, items last</b> so an agent reading a
    ///     truncated stream still sees the contract:
    ///     <c>{ detail, count?, has_more?, after_cursor?|next_page?, note?, items, &lt;sideloads&gt; }</c>.
    ///     <c>count</c> appears only when Zendesk supplied one; a tool emits either <c>after_cursor</c> or
    ///     <c>next_page</c>, never both (the pagination regime is per-tool). Sideloaded arrays keep their native
    ///     Zendesk names (they act as type labels) and are summary-projected; a sideload without a summary shape
    ///     fails visibly — omitted, with a note pointing at <c>detail:'full'</c>. Responses over the size budget
    ///     drop tail items, suppress the continuation token, force <c>has_more:true</c> and carry the recovery
    ///     recipe in the note.
    /// </summary>
    private static JsonElement BuildListEnvelope(JsonElement response, string itemsArrayName, string? itemShapeName,
        ZendeskDetail detail, bool cursorPagination, int? requestPage, int maxResponseChars, string? note,
        IReadOnlyCollection<string>? extraSummaryFields)
    {
        if (response.ValueKind is not JsonValueKind.Object)
            throw new InvalidOperationException("The Zendesk list response was not a JSON object.");
        var source = (JsonObject)JsonNode.Parse(response.GetRawText())!;

        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(note)) notes.Add(note.Trim());

        // Primary rows. Non-object items (for example tags_list's strings) pass through unchanged.
        var items = new JsonArray();
        if (source[itemsArrayName] is JsonArray sourceItems)
            foreach (var item in sourceItems)
                items.Add(ProjectItem(item, itemShapeName ?? itemsArrayName, detail, extraSummaryFields, notes));

        // Envelope metadata.
        long? count = source["count"] is JsonValue countValue && countValue.TryGetValue(out long countNumber)
            ? countNumber
            : null;
        var (hasMore, afterCursor, nextPage) = ResolveContinuation(source, cursorPagination, requestPage);

        // Sideloads: every other array in the Zendesk envelope. Unknown array names fail visibly in summary
        // mode (omitted + note) instead of leaking full objects; full mode includes them as full view.
        var sideloads = new List<KeyValuePair<string, JsonArray>>();
        foreach (var (name, value) in source)
        {
            if (name == itemsArrayName || value is not JsonArray sideload) continue;
            if (detail is ZendeskDetail.Summary && !SummaryShapes.ContainsKey(name))
            {
                notes.Add($"sideload {name} has no summary shape — use detail:'full'");
                continue;
            }

            var projected = new JsonArray();
            foreach (var item in sideload)
                projected.Add(item is JsonObject entity && detail is ZendeskDetail.Summary
                    ? SummarizeEntity(name, entity)
                    : ToFullViewNode(item));
            sideloads.Add(new KeyValuePair<string, JsonArray>(name, projected));
        }

        // Assemble, then apply the size guard: drop tail items (and, as a last resort, the sideloads), suppress
        // the continuation token, force has_more:true and put the recovery recipe in the note. A truncated
        // response never carries a continuation token — resuming from it would silently skip the dropped items.
        var envelope = Assemble(detail, count, hasMore, afterCursor, nextPage, ComposeNote(notes, null), items,
            sideloads);
        if (SerializedLength(envelope) <= maxResponseChars) return JsonSerializer.SerializeToElement(envelope);

        // Over budget: detach the reused items/sideload nodes from this candidate before the estimate and
        // truncation passes re-parent them into fresh envelopes (JsonNode forbids a node having two parents).
        envelope.Clear();

        var totalItems = items.Count;
        var keep = EstimateKeepCount(detail, count, notes, items, sideloads, totalItems, maxResponseChars,
            cursorPagination);
        if (keep < items.Count) items.RemoveRange(keep, items.Count - keep);

        // Verify the estimate exactly and shave the last 1-2 items only if the estimate ran slightly over (the
        // truncation note's own length shifts by a digit or two as the kept-count changes).
        envelope.Clear(); // detach the reused child nodes before assembling the truncated candidate
        envelope = AssembleTruncated(detail, count, notes, items, sideloads, totalItems, maxResponseChars,
            cursorPagination);
        while (SerializedLength(envelope) > maxResponseChars && items.Count > 0)
        {
            items.RemoveAt(items.Count - 1);
            envelope.Clear();
            envelope = AssembleTruncated(detail, count, notes, items, sideloads, totalItems, maxResponseChars,
                cursorPagination);
        }

        if (SerializedLength(envelope) > maxResponseChars && sideloads.Count > 0)
        {
            notes.Add("sideloaded arrays were also dropped — re-call without sideloads");
            envelope.Clear();
            envelope = Assemble(detail, count, true, null, null,
                ComposeNote(notes, TruncationNote(items.Count, totalItems, maxResponseChars, cursorPagination)), items,
                []);
        }

        return JsonSerializer.SerializeToElement(envelope);
    }

    /// <summary>
    ///     Estimates the largest number of leading items that keeps the truncated envelope within
    ///     <paramref name="maxResponseChars" />, serializing each projected item exactly once. The estimate uses
    ///     the fixed envelope overhead (metadata, note, sideloads, the <c>"items":[]</c> scaffolding) measured
    ///     once against per-item serialized lengths plus their comma separators; <see cref="BuildListEnvelope" />
    ///     then verifies the result exactly and shaves 1-2 more if needed. Returns a value in
    ///     <c>[0, items.Count]</c>.
    /// </summary>
    private static int EstimateKeepCount(ZendeskDetail detail, long? count, List<string> notes, JsonArray items,
        IReadOnlyList<KeyValuePair<string, JsonArray>> sideloads, int totalItems, int maxResponseChars,
        bool cursorPagination)
    {
        // Per-item serialized lengths (each item is serialized exactly once here).
        var itemLengths = new int[items.Count];
        for (var i = 0; i < items.Count; i++) itemLengths[i] = items[i]?.ToJsonString().Length ?? "null".Length;

        // Fixed overhead = a truncated envelope carrying an empty items array, minus that array's own "[]" (2).
        // The truncation note's kept-count digits are estimated against the total so the baseline note is at
        // least as long as any concrete one; the exact verify pass in the caller absorbs the small drift.
        var overheadEnvelope = Assemble(detail, count, true, null, null,
            ComposeNote(notes, TruncationNote(totalItems, totalItems, maxResponseChars, cursorPagination)),
            new JsonArray(), sideloads);
        var overhead = SerializedLength(overheadEnvelope) - 2;
        overheadEnvelope.Clear(); // detach the reused sideload nodes so they can be re-parented below

        // Grow the kept prefix while it (plus brackets and separators) still fits under the budget.
        var running = overhead + 2; // the empty items array's "[]"
        var keep = 0;
        for (var i = 0; i < itemLengths.Length; i++)
        {
            var next = running + itemLengths[i] + (keep > 0 ? 1 : 0); // +1 for the comma separator when not first
            if (next > maxResponseChars) break;
            running = next;
            keep++;
        }

        return keep;
    }

    /// <summary>
    ///     Assembles a truncated envelope: continuation token suppressed, <c>has_more:true</c> forced, and the
    ///     recovery recipe woven into the note (see <see cref="TruncationNote" />).
    /// </summary>
    private static JsonObject AssembleTruncated(ZendeskDetail detail, long? count, List<string> notes,
        JsonArray items, IReadOnlyList<KeyValuePair<string, JsonArray>> sideloads, int totalItems,
        int maxResponseChars, bool cursorPagination) =>
        Assemble(detail, count, true, null, null,
            ComposeNote(notes, TruncationNote(items.Count, totalItems, maxResponseChars, cursorPagination)), items,
            sideloads);

    /// <summary>
    ///     Resolves the envelope's continuation metadata for the tool's pagination regime: cursor endpoints read
    ///     <c>meta.has_more</c>/<c>meta.after_cursor</c>; offset endpoints derive <c>has_more</c> from Zendesk's
    ///     <c>next_page</c> being non-null and compute the next page <em>number</em> — URLs are never parsed.
    /// </summary>
    private static (bool? HasMore, string? AfterCursor, int? NextPage) ResolveContinuation(JsonObject source,
        bool cursorPagination, int? requestPage)
    {
        if (!cursorPagination)
        {
            var hasNextPage = source["next_page"] is not null;
            return (hasNextPage, null, hasNextPage ? (requestPage ?? 1) + 1 : null);
        }

        if (source["meta"] is not JsonObject meta ||
            meta["has_more"] is not JsonValue hasMoreValue || !hasMoreValue.TryGetValue(out bool hasMore))
            return (null, null, null);

        var afterCursor = hasMore && meta["after_cursor"] is JsonValue cursorValue &&
                          cursorValue.TryGetValue(out string? cursor) && !string.IsNullOrEmpty(cursor)
            ? cursor
            : null;
        return (hasMore, afterCursor, null);
    }

    /// <summary>Assembles the envelope in the metadata-first order the contract mandates.</summary>
    private static JsonObject Assemble(ZendeskDetail detail, long? count, bool? hasMore, string? afterCursor,
        int? nextPage, string? note, JsonArray items, IReadOnlyList<KeyValuePair<string, JsonArray>> sideloads)
    {
        var envelope = new JsonObject
        {
            ["detail"] = detail is ZendeskDetail.Full ? "full" : "summary"
        };
        if (count is { } totalCount) envelope["count"] = totalCount;
        if (hasMore is { } more) envelope["has_more"] = more;
        if (afterCursor is not null) envelope["after_cursor"] = afterCursor;
        else if (nextPage is { } page) envelope["next_page"] = page;
        if (!string.IsNullOrEmpty(note)) envelope["note"] = note;
        envelope["items"] = items;
        foreach (var (name, sideload) in sideloads) envelope[name] = sideload;
        return envelope;
    }

    private static string? ComposeNote(List<string> notes, string? truncationNote)
    {
        var all = truncationNote is null ? notes : [.. notes, truncationNote];
        return all.Count == 0 ? null : string.Join("; ", all);
    }

    /// <summary>The size-guard recovery recipe, naming the exact re-call that retrieves the dropped items.</summary>
    private static string TruncationNote(int kept, int total, int maxResponseChars, bool cursorPagination) =>
        kept > 0
            ? $"response exceeded {maxResponseChars} chars; items {kept + 1}–{total} of this page were dropped " +
              $"— re-call with {(cursorPagination ? "pageSize" : "perPage")}:{kept} or a narrower query to fetch them"
            : $"response exceeded {maxResponseChars} chars; all {total} items of this page were dropped — " +
              "re-call with a narrower query or detail:'summary'";

    private static int SerializedLength(JsonObject envelope) => envelope.ToJsonString().Length;

    /// <summary>
    ///     Projects one primary row: full view in <c>detail:'full'</c>; otherwise the array's summary shape —
    ///     with <c>results</c> (search) dispatching <b>per item</b> on <c>result_type</c>, never per array.
    /// </summary>
    private static JsonNode? ProjectItem(JsonNode? item, string shapeName, ZendeskDetail detail,
        IReadOnlyCollection<string>? extraFields, List<string> notes)
    {
        if (detail is ZendeskDetail.Full) return ToFullViewNode(item);
        if (item is not JsonObject entity) return item?.DeepClone();
        if (shapeName == "results") return SummarizeSearchResult(entity, extraFields, notes);

        return SummarizeEntity(shapeName, entity, extraFields)
               ?? throw new InvalidOperationException(
                   $"No summary shape is registered for the '{shapeName}' array — register one in " +
                   $"{nameof(ZendeskLean)} or route the tool through the full view.");
    }

    /// <summary>
    ///     Summarizes one heterogeneous search row by its <c>result_type</c>, keeping <c>result_type</c> on the
    ///     row so the agent can tell the entity kinds apart. Unmapped result types fail visibly: the row is
    ///     returned in full view and the note says so.
    /// </summary>
    private static JsonNode? SummarizeSearchResult(JsonObject entity, IReadOnlyCollection<string>? extraFields,
        List<string> notes)
    {
        var resultType = entity["result_type"] is JsonValue typeValue && typeValue.TryGetValue(out string? type)
            ? type
            : null;
        if (resultType is null || !SearchResultShapes.TryGetValue(resultType, out var shapeName))
        {
            var unmappedNote =
                $"result_type '{resultType ?? "unknown"}' has no summary shape — those rows are returned in full";
            if (!notes.Contains(unmappedNote)) notes.Add(unmappedNote);
            return ToFullViewNode(entity);
        }

        var row = SummarizeEntity(shapeName, entity, extraFields)!;
        row["result_type"] = resultType;
        return row;
    }

    private static void AppendExtraFields(JsonObject entity, JsonObject row,
        IReadOnlyCollection<string>? extraFields)
    {
        if (extraFields is null) return;
        foreach (var field in extraFields)
            if (entity[field] is { } value)
                row[field] = value.DeepClone();
    }

    /// <summary>Copies the allowlisted fields that are present and non-null, preserving the given order.</summary>
    private static void Copy(JsonObject source, JsonObject target, params string[] fields)
    {
        foreach (var field in fields)
            if (source[field] is { } value)
                target[field] = value.DeepClone();
    }

    /// <summary>Copies a string field truncated to <paramref name="maxChars" />; empty/absent values are omitted.</summary>
    private static void CopyTruncated(JsonObject source, JsonObject target, string field, int maxChars)
    {
        if (source[field] is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrEmpty(text))
            target[field] = Truncate(text, maxChars);
    }

    private static JsonObject SummarizeTicket(JsonObject ticket)
    {
        var row = new JsonObject();
        Copy(ticket, row, "id", "subject");
        // The description IS the first comment (tool guidance says so); the excerpt is enough to triage.
        CopyTruncated(ticket, row, "description", TicketDescriptionChars);
        Copy(ticket, row, "status", "priority", "type", "due_at", "created_at", "updated_at", "requester_id",
            "assignee_id", "group_id", "organization_id", "custom_status_id", "ticket_form_id", "problem_id",
            "external_id", "tags");
        if (ticket["via"] is JsonObject via && via["channel"] is { } channel)
            row["via"] = new JsonObject { ["channel"] = channel.DeepClone() };
        return row;
    }

    private static JsonObject SummarizeUser(JsonObject user)
    {
        var row = new JsonObject();
        Copy(user, row, "id", "name", "email", "role", "active", "suspended", "organization_id", "phone",
            "last_login_at", "external_id");
        return row;
    }

    private static JsonObject SummarizeOrganization(JsonObject organization)
    {
        var row = new JsonObject();
        Copy(organization, row, "id", "name", "domain_names", "external_id", "shared_tickets", "shared_comments",
            "tags", "created_at", "updated_at");
        return row;
    }

    private static JsonObject SummarizeArticle(JsonObject article)
    {
        var row = new JsonObject();
        // html_url is the human permalink — the KB citation/escalation pointer — and is always kept. The
        // snippet is a passthrough: Help Center search materializes it (with <em> relevance markers).
        Copy(article, row, "id", "title", "html_url", "section_id", "locale", "draft", "promoted", "label_names",
            "updated_at", "snippet");
        return row;
    }

    private static JsonObject SummarizeGroup(JsonObject group)
    {
        var row = new JsonObject();
        Copy(group, row, "id", "name", "description", "default", "deleted", "is_public");
        return row;
    }

    private static JsonObject SummarizeMacro(JsonObject macro)
    {
        // Actions (the bulk of a macro's tokens) are deliberately stripped — macros_get is the detail sink.
        var row = new JsonObject();
        Copy(macro, row, "id", "title", "active", "description", "usage_7d", "usage_30d");
        return row;
    }

    private static JsonObject SummarizeView(JsonObject view)
    {
        // Conditions/execution/restriction are stripped — views_get is the detail sink.
        var row = new JsonObject();
        Copy(view, row, "id", "title", "active", "default", "position");
        return row;
    }

    private static JsonObject SummarizeTicketField(JsonObject ticketField)
    {
        // Options are stripped and replaced by a computed count — ticket_fields_get carries the option values.
        var row = new JsonObject();
        Copy(ticketField, row, "id", "type", "title", "active", "required");
        if (ticketField["custom_field_options"] is JsonArray customOptions) row["options_count"] = customOptions.Count;
        else if (ticketField["system_field_options"] is JsonArray systemOptions)
            row["options_count"] = systemOptions.Count;
        return row;
    }

    private static JsonObject SummarizeTicketForm(JsonObject ticketForm)
    {
        // Condition trees are stripped — forms_get is the detail sink.
        var row = new JsonObject();
        Copy(ticketForm, row, "id", "name", "active", "default", "position", "ticket_field_ids");
        return row;
    }

    private static JsonObject SummarizeBrand(JsonObject brand)
    {
        // The logo (a full attachment object with thumbnails) and signature template are stripped.
        var row = new JsonObject();
        Copy(brand, row, "id", "name", "subdomain", "active", "default", "has_help_center");
        return row;
    }

    private static JsonObject SummarizeCustomStatus(JsonObject customStatus)
    {
        var row = new JsonObject();
        Copy(customStatus, row, "id", "status_category", "agent_label", "active");
        return row;
    }

    private static JsonObject SummarizeSuspendedTicket(JsonObject suspendedTicket)
    {
        // The raw inbound email (`content`) is stripped — suspended_tickets_get is the sink for it.
        var row = new JsonObject();
        Copy(suspendedTicket, row, "id", "subject", "cause");
        if (suspendedTicket["author"] is JsonObject author)
        {
            var authorRow = new JsonObject();
            Copy(author, authorRow, "name", "email");
            if (authorRow.Count > 0) row["author"] = authorRow;
        }

        Copy(suspendedTicket, row, "brand_id", "ticket_id", "created_at");
        return row;
    }

    private static JsonObject SummarizeIdentity(JsonObject identity)
    {
        var row = new JsonObject();
        Copy(identity, row, "id", "user_id", "type", "value", "primary", "verified");
        return row;
    }

    private static JsonObject SummarizeAttachment(JsonObject attachment)
    {
        var row = new JsonObject();
        Copy(attachment, row, "id", "file_name", "content_type", "size", "inline", "malware_scan_result");
        return row;
    }

    private static JsonObject SummarizeSideConversation(JsonObject sideConversation)
    {
        var row = new JsonObject();
        Copy(sideConversation, row, "id", "subject", "state", "created_at", "message_added_at");
        if (sideConversation["participants"] is JsonArray participants)
        {
            var participantRows = new JsonArray();
            foreach (var participant in participants)
            {
                if (participant is not JsonObject participantObject) continue;
                var participantRow = new JsonObject();
                Copy(participantObject, participantRow, "user_id", "email");
                participantRows.Add(participantRow);
            }

            row["participants"] = participantRows;
        }

        CopyTruncated(sideConversation, row, "preview_text", ExcerptChars);
        return row;
    }

    private static JsonObject SummarizeJobStatus(JsonObject jobStatus)
    {
        var row = new JsonObject();
        Copy(jobStatus, row, "id", "status", "progress", "total", "message");
        // The nested per-item `results` array is the heavy part; it collapses to counts plus the first few
        // failures. The full failure list stays reachable via job_statuses_get_many detail:'full'.
        if (jobStatus["results"] is JsonArray results)
        {
            var succeeded = 0;
            var failed = 0;
            var failures = new JsonArray();
            foreach (var result in results)
            {
                if (result is not JsonObject entry) continue;
                if (!IsJobFailure(entry))
                {
                    succeeded++;
                    continue;
                }

                failed++;
                if (failures.Count >= MaxJobFailuresInSummary) continue;
                var failure = new JsonObject();
                Copy(entry, failure, "index", "id");
                failure["error"] = Truncate(DescribeJobError(entry), ExcerptChars);
                failures.Add(failure);
            }

            var summary = new JsonObject { ["succeeded"] = succeeded, ["failed"] = failed };
            if (failures.Count > 0) summary["failures"] = failures;
            row["results_summary"] = summary;
        }

        return row;
    }

    private static bool IsJobFailure(JsonObject entry) =>
        (entry["success"] is JsonValue success && success.TryGetValue(out bool succeeded) && !succeeded)
        || entry["error"] is not null || entry["errors"] is not null;

    private static string DescribeJobError(JsonObject entry)
    {
        var error = entry["error"] ?? entry["errors"] ?? entry["details"];
        if (error is null) return "failed";
        return error is JsonValue value && value.TryGetValue(out string? text) ? text : error.ToJsonString();
    }

    private static JsonObject SummarizeAudit(JsonObject audit)
    {
        var row = new JsonObject();
        Copy(audit, row, "id", "created_at", "author_id");
        // via.source carries rule/trigger attribution (only present on system channels); the per-audit
        // forensic metadata (ip/location) is deliberately dropped.
        if (audit["via"] is JsonObject via)
        {
            var viaRow = new JsonObject();
            if (via["channel"] is { } channel) viaRow["channel"] = channel.DeepClone();
            if (via["source"] is JsonObject viaSource)
            {
                var sourceRow = new JsonObject();
                if (viaSource["rel"] is { } rel) sourceRow["rel"] = rel.DeepClone();
                if (viaSource["from"] is JsonObject from)
                {
                    var fromRow = new JsonObject();
                    Copy(from, fromRow, "id", "title");
                    if (fromRow.Count > 0) sourceRow["from"] = fromRow;
                }

                if (sourceRow.Count > 0) viaRow["source"] = sourceRow;
            }

            if (viaRow.Count > 0) row["via"] = viaRow;
        }

        if (audit["events"] is JsonArray events)
        {
            var eventRows = new JsonArray();
            foreach (var auditEvent in events)
                if (auditEvent is JsonObject eventObject)
                    eventRows.Add(SummarizeAuditEvent(eventObject));
            row["events"] = eventRows;
        }

        return row;
    }

    /// <summary>
    ///     Summarizes one audit event. Comment events collapse their triple body duplication
    ///     (body/html_body/plain_body) to a single excerpt of <c>plain_body ?? body</c>; VoiceComment events keep
    ///     only their identity (tickets_comments_list is the sink for the transcription/recording detail).
    /// </summary>
    private static JsonObject SummarizeAuditEvent(JsonObject auditEvent)
    {
        var type = auditEvent["type"] is JsonValue typeValue && typeValue.TryGetValue(out string? typeName)
            ? typeName
            : null;
        var row = new JsonObject();
        switch (type)
        {
            case "Comment":
                Copy(auditEvent, row, "id", "type", "public");
                var excerptSource = auditEvent["plain_body"] is JsonValue plainBody &&
                                    plainBody.TryGetValue(out string? plainText) && !string.IsNullOrEmpty(plainText)
                    ? plainText
                    : auditEvent["body"] is JsonValue body && body.TryGetValue(out string? bodyText)
                        ? bodyText
                        : null;
                if (!string.IsNullOrEmpty(excerptSource)) row["excerpt"] = Truncate(excerptSource, ExcerptChars);
                break;
            case "VoiceComment":
                Copy(auditEvent, row, "id", "type", "public");
                break;
            default:
                Copy(auditEvent, row, "id", "type", "field_name");
                // A change event's before/after values can be long free text (e.g. a description edit). When the
                // value is a JSON string, truncate it with the recovery marker; non-string values (tag arrays,
                // SLA-policy objects, numeric ids) are copied verbatim — they are already compact and structural.
                CopyAuditValue(auditEvent, row, "previous_value");
                CopyAuditValue(auditEvent, row, "value");
                break;
        }

        return row;
    }

    /// <summary>
    ///     Copies an audit change-event value field: JSON strings are truncated with the recovery marker
    ///     (<c>tickets_audits_list detail:'full'</c>); every other JSON kind (arrays, objects, numbers, booleans,
    ///     null) is copied verbatim.
    /// </summary>
    private static void CopyAuditValue(JsonObject source, JsonObject target, string field)
    {
        if (source[field] is not { } value) return;
        target[field] = value is JsonValue jsonValue && jsonValue.TryGetValue(out string? text)
            ? TruncateWithMarker(text, ExcerptChars, "tickets_audits_list detail:'full'")
            : value.DeepClone();
    }

    private static JsonObject SummarizeCustomObject(JsonObject customObject)
    {
        var row = new JsonObject();
        Copy(customObject, row, "key", "title", "title_pluralized", "description", "created_at", "updated_at");
        return row;
    }

    private static JsonObject SummarizeCustomObjectRecord(JsonObject record)
    {
        // custom_object_fields (the business data map) is copied verbatim — it is the reason to fetch the record.
        var row = new JsonObject();
        Copy(record, row, "id", "name", "custom_object_key", "external_id", "custom_object_fields", "created_at",
            "updated_at", "created_by_user_id", "updated_by_user_id");
        return row;
    }

    private static JsonObject SummarizeCommunityPost(JsonObject post)
    {
        // The post body (details) is the heavy field and is stripped — community_posts_search is a discovery tool;
        // read the full post at its html_url.
        var row = new JsonObject();
        Copy(post, row, "id", "title", "html_url", "status", "author_id", "topic_id", "created_at", "updated_at",
            "comment_count", "vote_sum", "pinned", "closed");
        return row;
    }

    private static JsonObject SummarizeDeletedTicket(JsonObject deletedTicket)
    {
        // Already minimal on the wire; actor {id,name} is small and copied verbatim. Anything outside the
        // allowlist (self-links, raw_subject, …) is dropped.
        var row = new JsonObject();
        Copy(deletedTicket, row, "id", "subject", "actor", "deleted_at", "previous_state");
        return row;
    }

    private static JsonObject SummarizeSatisfactionRating(JsonObject rating)
    {
        var row = new JsonObject();
        Copy(rating, row, "id", "score", "comment", "reason", "reason_code", "ticket_id", "requester_id",
            "assignee_id", "group_id", "created_at", "updated_at");
        return row;
    }

    private static JsonObject SummarizeSectionOrCategory(JsonObject sectionOrCategory)
    {
        var row = new JsonObject();
        Copy(sectionOrCategory, row, "id", "name", "html_url");
        CopyTruncated(sectionOrCategory, row, "description", ExcerptChars);
        Copy(sectionOrCategory, row, "category_id", "parent_section_id", "position", "updated_at");
        return row;
    }

    /// <summary>
    ///     The recursive full-view transform: drops null-valued properties, <c>url</c> string self-links
    ///     (<c>html_url</c> is untouched — different name), <c>next_page</c>/<c>previous_page</c> URL strings
    ///     (page <em>numbers</em> survive) and object-valued <c>links</c> pagination blocks. Array items are
    ///     preserved positionally, including nulls.
    /// </summary>
    private static JsonNode? ToFullViewNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject source:
            {
                var result = new JsonObject();
                foreach (var (name, value) in source)
                {
                    if (value is null || IsStrippedLink(name, value)) continue;
                    result[name] = ToFullViewNode(value);
                }

                return result;
            }
            case JsonArray array:
            {
                var result = new JsonArray();
                foreach (var item in array) result.Add(ToFullViewNode(item));
                return result;
            }
            default:
                return node?.DeepClone();
        }
    }

    private static bool IsStrippedLink(string name, JsonNode value) =>
        name switch
        {
            "url" or "next_page" or "previous_page" => value is JsonValue text && text.TryGetValue<string>(out _),
            "links" => value is JsonObject,
            _ => false
        };

    /// <summary>The number of leading characters to keep so a cut never splits a surrogate pair.</summary>
    private static int KeptChars(string value, int maxChars) =>
        maxChars > 0 && char.IsHighSurrogate(value[maxChars - 1]) ? maxChars - 1 : maxChars;

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<br\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakTagRegex();

    [GeneratedRegex(
        @"</?(p|div|li|ul|ol|h[1-6]|tr|table|thead|tbody|blockquote|pre|section|article|header|footer|figure|figcaption|dt|dd|dl)\b(?:[^>""']|""[^""]*""|'[^']*')*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockTagRegex();

    // Match only real HTML tag starts (optional '/', then a letter), attribute-quote-aware so a '>' inside a
    // quoted value can't end the tag early. Bare angle brackets in prose ("wait < 24 hours > SLA") are left
    // intact. A '<tag>'-shaped token in prose (e.g. "List<string>") is indistinguishable from markup and is
    // still stripped — the inherent limit of a dependency-free, non-parser transform.
    [GeneratedRegex("</?[a-zA-Z](?:[^>\"']|\"[^\"]*\"|'[^']*')*>")]
    private static partial Regex AnyTagRegex();

    [GeneratedRegex(@"[ \t\f\u00A0]+")]
    private static partial Regex HorizontalWhitespaceRegex();

    [GeneratedRegex(@" ?\n ?")]
    private static partial Regex SpaceAroundNewlineRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex NewlineRunRegex();
}