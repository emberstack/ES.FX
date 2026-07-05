using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ES.FX.Zendesk.MCP.Host.Configuration;
using ES.FX.Zendesk.Support;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ES.FX.Zendesk.MCP.Host.Tools;

/// <summary>
///     MCP tools for Zendesk ticket field definitions. Namespaced <c>ticket_fields_*</c>.
/// </summary>
/// <remarks>
///     Reads return the raw Zendesk JSON (<see cref="ZendeskKiotaRequests.SendForJsonAsync" />) instead of
///     round-tripping the generated models: Kiota re-serialization omits spec-read-only properties — the field and
///     option <c>id</c>s and the offset envelope's <c>count</c>/<c>next_page</c> — which are exactly the values
///     these tools exist to surface. List responses are then projected through <see cref="ZendeskLean" /> into the
///     uniform lean envelope — summary rows with a computed <c>options_count</c> by default, complete definitions
///     via <c>detail:'full'</c> or the per-record tools — and the full-detail tools cap each field's drop-down
///     options with a self-describing <c>options_truncated</c> marker (<c>ticket_fields_options_list</c> pages the
///     complete set).
/// </remarks>
[McpServerToolType]
public sealed class ZendeskTicketFieldTools(
    ZendeskSupportApiClient zendesk,
    IRequestAdapter requestAdapter,
    IOptionsMonitor<McpOptions> mcpOptions)
{
    /// <summary>Zendesk's documented maximum for <c>show_many</c>; larger lists are rejected with 400.</summary>
    private const int MaxIdsPerShowManyRequest = 100;

    /// <summary>
    ///     The per-field option cap on the full-detail tools (<c>ticket_fields_get(_many)</c>): 100 options cover
    ///     the common drop-downs, and a capped tail stays reachable via <c>ticket_fields_options_list</c>.
    /// </summary>
    private const int MaxOptionsPerField = 100;

    /// <summary>The uniform <c>detail</c> parameter description shared by the list tools.</summary>
    private const string DetailDescription =
        "Row detail: 'summary' (default, lean triage rows) | 'full' (complete records minus null fields and API " +
        "links).";

    /// <summary>Lists ticket field definitions.</summary>
    [McpServerTool(Name = "ticket_fields_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Ticket field definitions as lean summary rows (id, type, title, active, required, computed " +
        "options_count) — maps custom field ids to human titles. Option values NOT on rows: decode a ticket's " +
        "custom_fields via ticket_fields_get_many, or page one field's options via ticket_fields_options_list. " +
        "Inactive fields hidden by default (note reports count hidden); activeOnly:false includes them. Filter " +
        "applied per page AFTER fetch (no server-side active filter), so a page may carry fewer than pageSize " +
        "rows while more pages remain. Cursor pagination via has_more/after_cursor. detail:'full' for complete " +
        "definitions, or ticket_fields_get for one.")]
    public Task<JsonElement> List(
        [Description("Results per page (default 50; Zendesk caps at 100).")]
        int? pageSize = 50,
        [Description(
            "Continuation cursor for the NEXT page — OMIT for first page. When present, the opaque token copied " +
            "verbatim from previous response's after_cursor; not a page number, do not guess or pass empty " +
            "(invalid cursor → 400).")]
        string? afterCursor = null,
        [Description(
            "true (default): hide inactive field definitions (MCP-side per-page post-filter; note reports count " +
            "hidden). false: include inactive fields.")]
        bool activeOnly = true,
        CancellationToken cancellationToken = default,
        [Description(DetailDescription)] string detail = "summary")
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var parsedDetail = ZendeskLean.ParseDetail(detail);
            // Escape hatch for a generator (not spec) gap: the OAS models cursor pagination here
            // (components/parameters/CursorPaginationPage — a deepObject 'page' with size/after/before), but
            // Kiota flattens it to a single scalar 'page' query parameter that cannot emit the bracketed
            // page[size]/page[after] pair, so the values go on via WithCursorPagination.
            var request = zendesk.Api.V2.Ticket_fields.ToGetRequestInformation()
                .WithCursorPagination(pageSize, afterCursor);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            var (filtered, note) = ApplyActiveOnlyFilter(json, activeOnly);
            return ZendeskLean.BuildCursorListEnvelope(filtered, "ticket_fields", parsedDetail,
                MaxResponseChars("ticket_fields_list"), note);
        });

    /// <summary>Returns a single ticket field definition by id.</summary>
    [McpServerTool(Name = "ticket_fields_get", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Single ticket field definition by numeric id — full-detail record (null fields and API self-links " +
        "omitted): title, type, portal visibility/requirement flags, validation, and for drop-down " +
        "(tagger|multiselect) fields the option value→label pairs. Options capped at 100 — a capped field " +
        "carries an options_truncated marker; ticket_fields_options_list pages the complete set.")]
    public Task<JsonElement> Read(
        [Description("Numeric ticket field id.")]
        long id,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            var json = await requestAdapter.SendForJsonAsync(
                    zendesk.Api.V2.Ticket_fields[id].ToGetRequestInformation(), cancellationToken)
                .ConfigureAwait(false);
            if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("ticket_field", out var element) ||
                element.ValueKind is not JsonValueKind.Object)
                throw new McpException($"Zendesk ticket field '{id}' was not found.");

            var field = (JsonObject)JsonNode.Parse(element.GetRawText())!;
            CapFieldOptions(field, MaxOptionsPerField, OptionsListRecovery(field));
            return ZendeskLean.EnsureWithinBudget(
                ZendeskLean.ToFullView(JsonSerializer.SerializeToElement(field)),
                "ticket_fields_get", MaxResponseChars("ticket_fields_get"),
                "Record exceeds the response budget.");
        });

    /// <summary>Returns many ticket field definitions by id in one call.</summary>
    [McpServerTool(Name = "ticket_fields_get_many", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Many ticket field definitions by numeric id in one call — the one-call way to decode a ticket's " +
        "custom_fields: pass custom_fields[].id here instead of looping ticket_fields_get per field. Rows are " +
        "full-detail records (null fields and API self-links omitted); options capped at 100 per field — a " +
        "capped field carries an options_truncated marker; ticket_fields_options_list pages the complete set. " +
        "Hard cap 100 ids per call (Zendesk's show_many limit); for more, split into batches of 100, one call " +
        "per batch.")]
    public Task<JsonElement> ReadMany(
        [Description("Numeric ticket field ids, at most 100 per call — e.g. a ticket's custom_fields[].id.")]
        long[] ids,
        CancellationToken cancellationToken)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            // show_many rejects more than 100 ids with 400 Bad Request — surface the API contract as an
            // actionable batching instruction instead of silently fanning out server-side.
            if (ids.Length > MaxIdsPerShowManyRequest)
                throw new McpException(
                    $"ticket_fields_get_many accepts at most {MaxIdsPerShowManyRequest} ids per call (Zendesk's " +
                    $"show_many limit) but was passed {ids.Length}. Split the ids into batches of " +
                    $"{MaxIdsPerShowManyRequest} and call once per batch.");
            if (ids.Length == 0)
                return ZendeskLean.BuildOffsetListEnvelope(EmptyTicketFieldsResponse(), "ticket_fields",
                    null, ZendeskDetail.Full, MaxResponseChars("ticket_fields_get_many"));

            var request = zendesk.Api.V2.Ticket_fields.Show_many.ToGetRequestInformation(configuration =>
                configuration.QueryParameters.Ids = string.Join(',', ids));
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            // The rows are deliberately full view — this tool IS the detail sink for field definitions; only
            // the per-field option arrays are capped.
            return ZendeskLean.BuildOffsetListEnvelope(CapAllFieldOptions(json), "ticket_fields",
                null, ZendeskDetail.Full, MaxResponseChars("ticket_fields_get_many"));
        });

    /// <summary>Lists the custom options of a drop-down ticket field.</summary>
    [McpServerTool(Name = "ticket_fields_options_list", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Custom options of a drop-down ticket field (value→label pairs in the custom_field_options envelope) — " +
        "see allowed values before setting the field on a ticket or editing options with " +
        "ticket_fields_options_create_or_update.")]
    public Task<JsonElement> Options(
        [Description(
            "Numeric ticket field id. Must be a drop-down field (type tagger|multiselect); other types have no " +
            "options to list.")]
        long ticketFieldId,
        [Description("1-based page number (optional).")]
        int? page = null,
        [Description(
            "Results per page (default 100, max 100 — offset pagination caps at 100; higher values capped). Total " +
            "in 'count'; when 'count' exceeds the rows returned, advance 'page' for the rest.")]
        int? perPage = 100,
        CancellationToken cancellationToken = default)
        => ZendeskToolInvoker.InvokeAsync(async () =>
        {
            // The generated builder exposes no query parameters here; the documented offset pair (page/per_page,
            // https://developer.zendesk.com/api-reference/introduction/pagination/) goes on via the escape hatch
            // (see the spec-anomaly ledger in src/ES.FX.Zendesk/OpenApi/README.md).
            var request = zendesk.Api.V2.Ticket_fields[ticketFieldId].OptionsPath.ToGetRequestInformation()
                .WithQuery("page", page)
                .WithQuery("per_page", perPage);
            var json = await requestAdapter.SendForJsonAsync(request, cancellationToken).ConfigureAwait(false);
            // Strip per-option url self-links and the envelope's pagination URLs while keeping each option's
            // id/value/name — the id is what ticket_fields_options_create_or_update needs.
            return ZendeskLean.ToFullView(json);
        });

    /// <summary>Resolves the response-size budget for a tool (see <see cref="McpToolsOptions.GetMaxResponseChars" />).</summary>
    private int MaxResponseChars(string toolName) => mcpOptions.CurrentValue.Tools.GetMaxResponseChars(toolName);

    /// <summary>
    ///     The <c>activeOnly</c> post-filter. Zendesk has no server-side active filter on ticket fields (verified
    ///     negative), so inactive rows are removed from the CURRENT page after fetch: Zendesk's pagination
    ///     metadata stays intact — a filtered page can carry fewer than pageSize rows while <c>has_more</c>
    ///     remains true — and the count is never synthesized from the filtered items. The returned note reports
    ///     how many rows were hidden and names the opt-out.
    /// </summary>
    private static (JsonElement Response, string? Note) ApplyActiveOnlyFilter(JsonElement response, bool activeOnly)
    {
        if (!activeOnly || response.ValueKind is not JsonValueKind.Object ||
            JsonNode.Parse(response.GetRawText()) is not JsonObject source ||
            source["ticket_fields"] is not JsonArray fields)
            return (response, null);

        var hidden = 0;
        for (var index = fields.Count - 1; index >= 0; index--)
        {
            if (fields[index] is not JsonObject field ||
                field["active"] is not JsonValue active || !active.TryGetValue(out bool isActive) || isActive)
                continue;
            fields.RemoveAt(index);
            hidden++;
        }

        return hidden == 0
            ? (response, null)
            : (JsonSerializer.SerializeToElement(source),
                $"{hidden} inactive field{(hidden == 1 ? string.Empty : "s")} hidden — pass activeOnly:false to " +
                "include them");
    }

    /// <summary>
    ///     Caps a field's <c>custom_field_options</c> at <paramref name="maxOptions" /> in place, replacing the
    ///     dropped tail with a self-describing <c>options_truncated</c> marker whose <paramref name="recovery" />
    ///     names the escalation path. Fields at or under the cap pass through untouched.
    /// </summary>
    internal static void CapFieldOptions(JsonObject field, int maxOptions, string recovery)
    {
        if (field["custom_field_options"] is not JsonArray options || options.Count <= maxOptions) return;
        var total = options.Count;
        while (options.Count > maxOptions) options.RemoveAt(options.Count - 1);
        field["options_truncated"] = $"showing {maxOptions} of {total} options — {recovery}";
    }

    /// <summary>The <c>options_truncated</c> recovery pointer for the read tools, naming the exact paging re-call.</summary>
    private static string OptionsListRecovery(JsonObject field) =>
        field["id"] is JsonValue id
            ? $"page the complete set with ticket_fields_options_list (ticketFieldId:{id.ToJsonString()})"
            : "page the complete set with ticket_fields_options_list";

    /// <summary>Caps every field's options in a <c>show_many</c> response (see <see cref="CapFieldOptions" />).</summary>
    private static JsonElement CapAllFieldOptions(JsonElement response)
    {
        if (response.ValueKind is not JsonValueKind.Object ||
            JsonNode.Parse(response.GetRawText()) is not JsonObject source ||
            source["ticket_fields"] is not JsonArray fields)
            return response;

        foreach (var field in fields)
            if (field is JsonObject fieldObject)
                CapFieldOptions(fieldObject, MaxOptionsPerField, OptionsListRecovery(fieldObject));
        return JsonSerializer.SerializeToElement(source);
    }

    /// <summary>An empty wire-shaped ticket_fields response, for the no-ids fast path of <c>ticket_fields_get_many</c>.</summary>
    private static JsonElement EmptyTicketFieldsResponse() =>
        JsonSerializer.SerializeToElement(new JsonObject { ["ticket_fields"] = new JsonArray() });
}